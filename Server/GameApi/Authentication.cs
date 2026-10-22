using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MagicOnline.GameApi;

public sealed record Credentials(string LoginId, string Password);
public sealed record TokenInput(string RefreshToken);
public sealed record AuthReply(string UserId, string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresAt);
public sealed class ApiFault(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
}

public sealed class Accounts(Database db)
{
    readonly PasswordHasher<string> hasher = new();
    readonly string dummyHash = new PasswordHasher<string>().HashPassword("dummy", "not-a-real-password");
    static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    static string Normalize(string value) => value.Trim().ToUpperInvariant();
    static void Validate(Credentials input)
    {
        if (input.LoginId == null || !Regex.IsMatch(input.LoginId.Trim(), "^[A-Za-z0-9_]{3,32}$") ||
            input.Password == null || input.Password.Length is < 8 or > 128)
            throw new ApiFault(400, "InvalidCredentialsFormat");
    }

    public async Task<AuthReply> Register(Credentials input)
    {
        Validate(input);
        var id = Guid.NewGuid(); string login = input.LoginId.Trim();
        string hash = hasher.HashPassword(id.ToString(), input.Password);
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        try
        {
            await using (var cmd = Database.Command(c, "INSERT INTO users(id,login_id,normalized_login,password_hash,nickname) VALUES($1,$2,$3,$4,$2)", id, login, Normalize(login), hash))
                await cmd.ExecuteNonQueryAsync();
            foreach (var sql in new[] { "INSERT INTO wallets(user_id) VALUES($1)", "INSERT INTO user_stats(user_id) VALUES($1)", "INSERT INTO wallet_ledger(user_id,request_key,coin_delta) VALUES($1,'InitialGrant',20)" })
            { await using var cmd = Database.Command(c, sql, id); await cmd.ExecuteNonQueryAsync(); }
            var reply = await Issue(c, id);
            await tx.CommitAsync(); return reply;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new ApiFault(409, "LoginIdTaken"); }
    }

    public async Task<AuthReply> Login(Credentials input)
    {
        Validate(input);
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        Guid id; string hash; bool disabled, locked;
        await using (var cmd = Database.Command(c, "SELECT id,password_hash,disabled,locked_until > now() FROM users WHERE normalized_login=$1 FOR UPDATE", Normalize(input.LoginId)))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync())
            {
                hasher.VerifyHashedPassword("dummy", dummyHash, input.Password);
                throw new ApiFault(401, "InvalidLogin");
            }
            id = r.GetGuid(0); hash = r.GetString(1); disabled = r.GetBoolean(2); locked = !r.IsDBNull(3) && r.GetBoolean(3);
        }
        if (disabled || locked) throw new ApiFault(401, "InvalidLogin");
        var verified = hasher.VerifyHashedPassword(id.ToString(), hash, input.Password);
        if (verified == PasswordVerificationResult.Failed)
        {
            await using var fail = Database.Command(c, "UPDATE users SET failed_logins=failed_logins+1,locked_until=CASE WHEN failed_logins+1>=5 THEN now()+interval '5 minutes' ELSE NULL END WHERE id=$1", id);
            await fail.ExecuteNonQueryAsync(); await tx.CommitAsync();
            throw new ApiFault(401, "InvalidLogin");
        }
        string newHash = verified == PasswordVerificationResult.SuccessRehashNeeded ? hasher.HashPassword(id.ToString(), input.Password) : hash;
        await using (var ok = Database.Command(c, "UPDATE users SET failed_logins=0,locked_until=NULL,password_hash=$2 WHERE id=$1", id, newHash)) await ok.ExecuteNonQueryAsync();
        var reply = await Issue(c, id);
        await tx.CommitAsync(); return reply;
    }

    static async Task<AuthReply> Issue(NpgsqlConnection c, Guid user)
    {
        string access = NewToken(), refresh = NewToken();
        var expires = DateTimeOffset.UtcNow.AddMinutes(15);
        await using var cmd = Database.Command(c, "INSERT INTO auth_sessions(id,user_id,access_hash,refresh_hash,access_expires,refresh_expires) VALUES($1,$2,$3,$4,$5,$6)", Guid.NewGuid(), user, Hash(access), Hash(refresh), expires, DateTimeOffset.UtcNow.AddDays(14));
        await cmd.ExecuteNonQueryAsync(); return new(user.ToString(), access, refresh, expires);
    }

    public async Task<AuthReply> Refresh(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 64) throw new ApiFault(401, "InvalidToken");
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        var tokenHash = Hash(token);
        Guid session = default, user = default; bool valid = false;
        await using (var cmd = Database.Command(c, "SELECT s.id,s.user_id,NOT s.revoked AND s.refresh_expires>now() AND NOT u.disabled FROM auth_sessions s JOIN users u ON u.id=s.user_id WHERE s.refresh_hash=$1 FOR UPDATE OF s", tokenHash))
        await using (var r = await cmd.ExecuteReaderAsync())
            if (await r.ReadAsync()) { session = r.GetGuid(0); user = r.GetGuid(1); valid = r.GetBoolean(2); }
        if (!valid)
        {
            // Reusing a rotated token revokes the whole session, including its latest access token.
            await using var revoke = Database.Command(c, "UPDATE auth_sessions SET revoked=true WHERE id IN (SELECT session_id FROM auth_refresh_uses WHERE token_hash=$1)", tokenHash);
            await revoke.ExecuteNonQueryAsync(); await tx.CommitAsync();
            throw new ApiFault(401, "InvalidToken");
        }
        string access = NewToken(), refresh = NewToken(); var expires = DateTimeOffset.UtcNow.AddMinutes(15);
        await using (var used = Database.Command(c, "INSERT INTO auth_refresh_uses(token_hash,session_id) VALUES($1,$2)", tokenHash, session)) await used.ExecuteNonQueryAsync();
        await using (var update = Database.Command(c, "UPDATE auth_sessions SET access_hash=$2,refresh_hash=$3,access_expires=$4 WHERE id=$1", session, Hash(access), Hash(refresh), expires)) await update.ExecuteNonQueryAsync();
        await tx.CommitAsync(); return new(user.ToString(), access, refresh, expires);
    }

    public async Task<(Guid User, Guid Session)?> Authenticate(string token)
    {
        if (token.Length != 64) return null;
        await using var cmd = db.Source.CreateCommand("SELECT s.user_id,s.id FROM auth_sessions s JOIN users u ON u.id=s.user_id WHERE s.access_hash=$1 AND s.access_expires>now() AND s.refresh_expires>now() AND NOT s.revoked AND NOT u.disabled");
        cmd.Parameters.AddWithValue(Hash(token));
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? (r.GetGuid(0), r.GetGuid(1)) : null;
    }
    public async Task Logout(Guid session)
    {
        await using var cmd = db.Source.CreateCommand("UPDATE auth_sessions SET revoked=true WHERE id=$1");
        cmd.Parameters.AddWithValue(session); await cmd.ExecuteNonQueryAsync();
    }
}

public sealed class SessionAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, Accounts accounts) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var session = await accounts.Authenticate(header[7..]);
        if (session == null) return AuthenticateResult.Fail("InvalidToken");
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, session.Value.User.ToString()), new Claim("session", session.Value.Session.ToString()) };
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }
}
