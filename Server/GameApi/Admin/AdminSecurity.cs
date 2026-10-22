using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace MagicOnline.GameApi.Admin;

public static class AdminSecurity
{
    public const string Scheme = "AdminCookie", Policy = "AdminRead";
    public static bool Enabled(IWebHostEnvironment env, IConfiguration config) => env.IsDevelopment() || config.GetValue<bool>("Admin:Enabled");
    public static bool LocalSetup(HttpContext context, IWebHostEnvironment env) => env.IsDevelopment() &&
        context.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip);
    public static bool DevelopmentLogin(HttpContext context, IWebHostEnvironment env) => LocalSetup(context, env) &&
        context.RequestServices.GetRequiredService<IConfiguration>().GetValue("Admin:PasswordlessDevelopment", true);
    public static void Add(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<AdminAccounts>();
        builder.Services.AddSingleton<AdminReadService>();
        builder.Services.AddSingleton<AdminMemberService>();
        builder.Services.AddSingleton<AdminChartService>();
        builder.Services.AddAuthentication().AddCookie(Scheme, options => {
            options.Cookie.Name = "MagicOnline.Admin";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.Path = "/";
            options.LoginPath = "/admin/login";
            options.AccessDeniedPath = "/admin/login";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = async context => {
                var accounts = context.HttpContext.RequestServices.GetRequiredService<AdminAccounts>();
                if (context.Principal?.HasClaim("admin_dev_login", "true") == true &&
                    !DevelopmentLogin(context.HttpContext, context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>()))
                { context.RejectPrincipal(); return; }
                if (!await accounts.IsValid(context.Principal)) context.RejectPrincipal();
            };
            options.Events.OnRedirectToLogin = context => {
                if (context.Request.Path.StartsWithSegments("/api/admin")) context.Response.StatusCode = 401;
                else context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization(options => options.AddPolicy(Policy, policy =>
            policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireRole("Owner", "Operator", "Viewer")));
        builder.Services.AddRazorPages(options => options.Conventions.AuthorizeFolder("/Admin", Policy));
        builder.Services.Configure<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>(options => {
            options.Cookie.Name = "MagicOnline.Admin.Csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
    }

    public static void Map(WebApplication app)
    {
        app.MapRazorPages();
        var group = app.MapGroup("/api/admin").RequireAuthorization(Policy);
        group.MapGet("/dashboard", (AdminReadService read) => read.Dashboard());
        group.MapGet("/members", (string? q, string? state, int? page, AdminReadService read) => read.Members(q, state, page ?? 1));
        group.MapGet("/members/{id:guid}", (Guid id, AdminReadService read) => read.Member(id));
        group.MapGet("/matches", (string? q, string? state, int? page, AdminReadService read) => read.Matches(q, state, page ?? 1));
        group.MapGet("/matches/{id}", (string id, AdminReadService read) => read.Match(id));
        group.MapGet("/wallet", (string? q, string? state, int? page, AdminReadService read) => read.Ledger(q, state, page ?? 1));
        group.MapGet("/charts", (string? name, AdminReadService read) => read.Chart(name ?? "SkillMatch"));
        group.MapGet("/audit", async (HttpContext ctx, int? page, AdminReadService read) => {
            if (!ctx.User.IsInRole("Owner")) return Results.Forbid(authenticationSchemes: new[] { Scheme });
            return Results.Ok(await read.Audit(page ?? 1));
        });
    }
}

public sealed class AdminAccounts(Database db)
{
    readonly PasswordHasher<string> hasher = new();
    readonly string dummy = new PasswordHasher<string>().HashPassword("dummy", "unused-dummy-password");
    static string Normalize(string login) => login.Trim().ToUpperInvariant();
    public async Task<bool> HasOwner()
    {
        await using var cmd = db.Source.CreateCommand("SELECT EXISTS(SELECT 1 FROM admin_users)");
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }
    public async Task CreateFirst(string login, string password, string requestId)
    {
        if (login == null || !Regex.IsMatch(login.Trim(), "^[A-Za-z0-9_]{3,32}$") || password == null || password.Length is < 12 or > 128)
            throw new ApiFault(400, "관리자 ID는 영문·숫자·밑줄 3~32자, 비밀번호는 12~128자로 입력해 주세요.");
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using (var gate = Database.Command(c, "SELECT pg_advisory_xact_lock(687451004)")) await gate.ExecuteNonQueryAsync();
        await using (var exists = Database.Command(c, "SELECT EXISTS(SELECT 1 FROM admin_users)"))
            if ((bool)(await exists.ExecuteScalarAsync())!) throw new ApiFault(409, "최초 관리자 설정이 이미 완료됐습니다.");
        var id = Guid.NewGuid();
        await using (var add = Database.Command(c, "INSERT INTO admin_users(id,login_id,normalized_login,password_hash,role) VALUES($1,$2,$3,$4,'Owner')", id, login.Trim(), Normalize(login), hasher.HashPassword(id.ToString(), password)))
            await add.ExecuteNonQueryAsync();
        await using (var log = Database.Command(c, "INSERT INTO admin_audit_logs(admin_id,action,target,request_id) VALUES($1,'AdminCreated',$2,$3)", id, id.ToString(), requestId)) await log.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
    public async Task<ClaimsPrincipal?> Login(string? login, string? password, string requestId)
    {
        if (string.IsNullOrWhiteSpace(login) || login.Length > 32 || password == null || password.Length > 128) return null;
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        Guid id; string hash, name, role; bool blocked;
        await using (var cmd = Database.Command(c, "SELECT id,password_hash,login_id,role,disabled OR development_only OR COALESCE(locked_until>now(),false) FROM admin_users WHERE normalized_login=$1 FOR UPDATE", Normalize(login)))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync()) { hasher.VerifyHashedPassword("dummy", dummy, password); return null; }
            id = r.GetGuid(0); hash = r.GetString(1); name = r.GetString(2); role = r.GetString(3); blocked = r.GetBoolean(4);
        }
        if (blocked) { hasher.VerifyHashedPassword("dummy", dummy, password); return null; }
        var verified = hasher.VerifyHashedPassword(id.ToString(), hash, password);
        if (verified == PasswordVerificationResult.Failed)
        {
            await using (var fail = Database.Command(c, "UPDATE admin_users SET failed_logins=failed_logins+1,locked_until=CASE WHEN failed_logins+1>=5 THEN now()+interval '5 minutes' ELSE NULL END WHERE id=$1", id)) await fail.ExecuteNonQueryAsync();
            await using (var log = Database.Command(c, "INSERT INTO admin_audit_logs(admin_id,action,target,request_id) VALUES($1,'LoginFailed',$2,$3)", id, id.ToString(), requestId)) await log.ExecuteNonQueryAsync();
            await tx.CommitAsync(); return null;
        }
        var session = Guid.NewGuid();
        await using (var update = Database.Command(c, "UPDATE admin_users SET failed_logins=0,locked_until=NULL,password_hash=$2 WHERE id=$1", id, verified == PasswordVerificationResult.SuccessRehashNeeded ? hasher.HashPassword(id.ToString(), password) : hash)) await update.ExecuteNonQueryAsync();
        await using (var add = Database.Command(c, "INSERT INTO admin_sessions(id,admin_id,expires_at) VALUES($1,$2,now()+interval '30 minutes')", session, id)) await add.ExecuteNonQueryAsync();
        await using (var log = Database.Command(c, "INSERT INTO admin_audit_logs(admin_id,action,target,request_id) VALUES($1,'LoginSucceeded',$2,$3)", id, session.ToString(), requestId)) await log.ExecuteNonQueryAsync();
        await tx.CommitAsync();
        return new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.Role, role), new Claim("admin_session", session.ToString()) }, AdminSecurity.Scheme));
    }
    public async Task<bool> IsValid(ClaimsPrincipal? principal)
    {
        if (!Guid.TryParse(principal?.FindFirstValue("admin_session"), out var session) || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var user)) return false;
        await using var cmd = db.Source.CreateCommand("SELECT EXISTS(SELECT 1 FROM admin_sessions s JOIN admin_users a ON a.id=s.admin_id WHERE s.id=$1 AND a.id=$2 AND NOT s.revoked AND NOT a.disabled AND s.expires_at>now() AND a.role=$3 AND s.development_only=$4 AND (NOT a.development_only OR $4))");
        cmd.Parameters.AddWithValue(session); cmd.Parameters.AddWithValue(user); cmd.Parameters.AddWithValue(principal.FindFirstValue(ClaimTypes.Role) ?? "");
        cmd.Parameters.AddWithValue(principal.HasClaim("admin_dev_login", "true"));
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }
    public async Task<ClaimsPrincipal?> LoginForDevelopment(HttpContext context, IWebHostEnvironment env, string? login)
    {
        // This bypass must remain unavailable from remote clients and production.
        if (!AdminSecurity.DevelopmentLogin(context, env)) throw new ApiFault(403, "DevelopmentLoginUnavailable");
        if (login == null || !Regex.IsMatch(login.Trim(), "^[A-Za-z0-9_]{3,32}$")) return null;
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using (var gate = Database.Command(c, "SELECT pg_advisory_xact_lock(687451004)")) await gate.ExecuteNonQueryAsync();
        Guid id = default; string name = "", role = "";
        await using (var cmd = Database.Command(c, "SELECT id,login_id,role,disabled OR COALESCE(locked_until>now(),false) FROM admin_users WHERE normalized_login=$1 FOR UPDATE", Normalize(login)))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (await r.ReadAsync())
            {
                if (r.GetBoolean(3)) return null;
                id=r.GetGuid(0); name=r.GetString(1); role=r.GetString(2);
            }
        }
        if (id == default)
        {
            await using (var exists = Database.Command(c, "SELECT EXISTS(SELECT 1 FROM admin_users)"))
                if ((bool)(await exists.ExecuteScalarAsync())!) return null;
            id=Guid.NewGuid(); name=login.Trim(); role="Owner";
            // No shared/temporary password. Production rejects development-only accounts.
            await using (var create = Database.Command(c, "INSERT INTO admin_users(id,login_id,normalized_login,password_hash,role,development_only) VALUES($1,$2,$3,'','Owner',true)", id,name,Normalize(name))) await create.ExecuteNonQueryAsync();
            await using (var audit = Database.Command(c, "INSERT INTO admin_audit_logs(admin_id,action,target,request_id) VALUES($1,'DevelopmentAdminCreated',$2,$3)",id,id.ToString(),context.TraceIdentifier)) await audit.ExecuteNonQueryAsync();
        }
        var session=Guid.NewGuid();
        await using (var add = Database.Command(c, "INSERT INTO admin_sessions(id,admin_id,expires_at,development_only) VALUES($1,$2,now()+interval '30 minutes',true)",session,id)) await add.ExecuteNonQueryAsync();
        await using (var audit = Database.Command(c, "INSERT INTO admin_audit_logs(admin_id,action,target,request_id) VALUES($1,'DevelopmentLoginSucceeded',$2,$3)",id,session.ToString(),context.TraceIdentifier)) await audit.ExecuteNonQueryAsync();
        await tx.CommitAsync();
        return new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim(ClaimTypes.Name,name),new Claim(ClaimTypes.Role,role),new Claim("admin_session",session.ToString()),new Claim("admin_dev_login","true")},AdminSecurity.Scheme));
    }
    public async Task Logout(ClaimsPrincipal principal, string requestId)
    {
        if (!Guid.TryParse(principal.FindFirstValue("admin_session"), out var session)) return;
        var user = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var c = await db.Source.OpenConnectionAsync(); await using var tx = await c.BeginTransactionAsync();
        await using (var cmd = Database.Command(c, "UPDATE admin_sessions SET revoked=true WHERE id=$1 AND admin_id=$2", session, user)) await cmd.ExecuteNonQueryAsync();
        await using (var log = Database.Command(c, "INSERT INTO admin_audit_logs(admin_id,action,target,request_id) VALUES($1,'Logout',$2,$3)", user, session.ToString(), requestId)) await log.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
}
