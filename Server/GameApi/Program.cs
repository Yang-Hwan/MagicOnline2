using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using MagicOnline.GameApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using MagicOnline.MatchSettlement;
using Assets.Scripts.Prack.BackSys.GameData;
using MagicOnline.GameApi.Admin;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
if (!builder.Environment.IsDevelopment() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PORT")))
    throw new InvalidOperationException("Local server requires ASPNETCORE_ENVIRONMENT=Development. Configure PORT for deployment.");
if (Environment.GetEnvironmentVariable("PORT") is { } port) builder.WebHost.UseUrls("http://0.0.0.0:" + int.Parse(port));
var connectionString = builder.Configuration.GetConnectionString("GameDb") ?? throw new InvalidOperationException("Configure ConnectionStrings:GameDb with dotnet user-secrets or ConnectionStrings__GameDb.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<Database>(); builder.Services.AddSingleton<Accounts>(); builder.Services.AddSingleton<GameData>();
builder.Services.AddSingleton<Attendance>();
builder.Services.AddSingleton<ISettlementStore, PostgresSettlementStore>();
builder.Services.AddSingleton<IServerClock, ServerClock>();
builder.Services.AddSingleton<SettlementService>(); builder.Services.AddSingleton<Matches>();
builder.Services.AddHostedService<MatchMaintenance>();
builder.Services.ConfigureHttpJsonOptions(o => {
    o.SerializerOptions.IncludeFields = true;
    o.SerializerOptions.PropertyNamingPolicy = null;
    o.SerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions, SessionAuthentication>("Session", null);
builder.Services.AddAuthorization();
AdminSecurity.Add(builder);
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("admin-login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.Use(async (context, next) => {
    bool admin = context.Request.Path.StartsWithSegments("/admin") || context.Request.Path.StartsWithSegments("/api/admin");
    if (admin)
    {
        if (!AdminSecurity.Enabled(app.Environment, app.Configuration)) { context.Response.StatusCode = 404; return; }
        context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; style-src 'self'; script-src 'none'; img-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'self'";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
    await next();
});
app.Use(async (context, next) => {
    context.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (ApiFault e) { context.Response.StatusCode = e.Status; await context.Response.WriteAsJsonAsync(new { Code = e.Message }); }
    catch (BadHttpRequestException e) { context.Response.StatusCode = e.StatusCode; await context.Response.WriteAsJsonAsync(new { Code = "InvalidRequest" }); }
    catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation || e.SqlState == PostgresErrorCodes.CheckViolation || e.SqlState == PostgresErrorCodes.ForeignKeyViolation)
    { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { Code = "DataConflict" }); }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
    {
        context.Response.StatusCode = e is UnauthorizedAccessException ? 403 : e is KeyNotFoundException ? 404 : 409;
        await context.Response.WriteAsJsonAsync(new { Code = "RequestRejected" });
    }
    catch (Exception e)
    {
        if (e is PostgresException pg) app.Logger.LogError("Database request failed: {SqlState}, {Message}", pg.SqlState, pg.MessageText);
        else app.Logger.LogError("Request failed ({Type})", e.GetType().Name);
        context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { Code = "ServerError" });
    }
});
app.UseStaticFiles();
app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
AdminSecurity.Map(app);
app.MapGet("/health", async (Database db) => {
    await using var cmd = db.Source.CreateCommand("SELECT 1"); await cmd.ExecuteScalarAsync();
    return Results.Ok(new { Ok = true, Database = "Connected", ApiVersion = 1, MatchProtocolVersion = MatchRulesContract.CurrentVersion });
});
app.MapGet("/api/match-protocol", () => new { Version = MatchRulesContract.CurrentVersion }).RequireAuthorization();
app.MapPost("/api/auth/register", (Credentials input, Accounts accounts) => accounts.Register(input)).RequireRateLimiting("auth");
app.MapPost("/api/auth/login", (Credentials input, Accounts accounts) => accounts.Login(input)).RequireRateLimiting("auth");
app.MapPost("/api/auth/refresh", (TokenInput input, Accounts accounts) => accounts.Refresh(input.RefreshToken)).RequireRateLimiting("auth");
app.MapPost("/api/auth/logout", async (ClaimsPrincipal user, Accounts accounts) => { await accounts.Logout(Guid.Parse(user.FindFirstValue("session")!)); return Results.NoContent(); }).RequireAuthorization();
app.MapGet("/api/me", (ClaimsPrincipal user, GameData data) => data.Profile(UserId(user))).RequireAuthorization();
app.MapPatch("/api/me/profile", async (ClaimsPrincipal user, ProfileEdit edit, GameData data) => { await data.EditProfile(UserId(user), edit); return await data.Profile(UserId(user)); }).RequireAuthorization();
app.MapGet("/api/players/{id:guid}", async (Guid id, GameData data) => {
    var p = await data.Profile(id);
    return new { p.InDate, p.NickName, p.AvatarIdx, p.LvVel, p.Coin, p.Ball_4_Win, p.Ball_4_Lose, p.Ball_4_Draw, p.Ball_3_Win, p.Ball_3_Lose, p.Ball_3_Draw };
}).RequireAuthorization();
app.MapGet("/api/charts/{name}", (string name, GameData data) => data.Chart(name)).RequireAuthorization();
app.MapPost("/api/me/attendance", (ClaimsPrincipal user, Attendance attendance) => attendance.CheckIn(UserId(user))).RequireAuthorization();
app.MapGet("/api/me/history", (ClaimsPrincipal user, Matches matches) => matches.History(UserId(user))).RequireAuthorization();
app.MapPost("/api/matches", async (ClaimsPrincipal user, MatchRegistration input, Matches matches) => new { MatchId = await matches.Register(UserId(user), input) }).RequireAuthorization();
app.MapGet("/api/matches/{id}", (string id, ClaimsPrincipal user, Matches matches) => matches.Status(id, UserId(user))).RequireAuthorization();
app.MapPost("/api/matches/{id}/ready", (string id, ClaimsPrincipal user, MatchReady input, Matches matches) => new { Playing = matches.Ready(id, UserId(user), input) }).RequireAuthorization();
app.MapPost("/api/matches/{id}/heartbeat", (string id, ClaimsPrincipal user, SettlementService service) => { service.Heartbeat(id, UserId(user).ToString()); return Results.NoContent(); }).RequireAuthorization();
app.MapPost("/api/matches/{id}/result", (string id, ResultReport report, ClaimsPrincipal user, SettlementService service) => Results.Ok(service.ConfirmResult(id, UserId(user).ToString(), report))).RequireAuthorization();
app.MapPost("/api/matches/{id}/surrender", (string id, ClaimsPrincipal user, SettlementService service) => Results.Ok(service.Surrender(id, UserId(user).ToString()))).RequireAuthorization();
app.MapPost("/api/matches/{id}/disconnect", (string id, ClaimsPrincipal user, SettlementService service) => Results.Ok(service.EvaluateDisconnect(id, UserId(user).ToString()))).RequireAuthorization();
await app.Services.GetRequiredService<Database>().Migrate();
if (!args.Contains("--migrate-only")) await app.RunAsync();
static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
