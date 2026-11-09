using System.Security.Claims;
using System.Text.RegularExpressions;
using Npgsql;

namespace PsyPath.Api;

public record AuthRequest(string Login, string Password, string? DisplayName);
public record UserDto(long Id, string Login, string DisplayName, string Role);
public record AuthResponse(string Token, UserDto User);

public static partial class AuthEndpoints
{
    [GeneratedRegex("^[A-Za-z0-9_.-]{3,32}$")]
    private static partial Regex LoginRx();

    public static void MapAuth(this WebApplication app)
    {
        var g = app.MapGroup("/api/auth").RequireRateLimiting("auth");
        g.MapPost("/register", Register);
        g.MapPost("/login", Login);
        // без действующего токена сюда не попасть, поэтому лимит попыток не нужен
        app.MapPost("/api/auth/refresh", Refresh).RequireAuthorization();
    }

    // приложение продлевает токен при запуске: активного пользователя не выкидывает через 30 дней
    static async Task<IResult> Refresh(ClaimsPrincipal me, NpgsqlDataSource db, Tokens tokens)
    {
        await using var cmd = db.CreateCommand(
            "select id, login, display_name, role, is_blocked from psypath.users where id = $1");
        cmd.Parameters.AddWithValue(me.UserId());
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return Errors.Unauthorized("Пользователь не найден");
        if (r.GetBoolean(4)) return Errors.Forbidden("Аккаунт заблокирован");

        var user = new UserDto(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3));
        return Results.Ok(new AuthResponse(tokens.Create(user.Id, user.Role), user));
    }

    static async Task<IResult> Register(AuthRequest req, NpgsqlDataSource db, Tokens tokens)
    {
        string login = (req.Login ?? "").Trim();
        string name = (req.DisplayName ?? "").Trim();
        if (!LoginRx().IsMatch(login))
            return Errors.Bad("Логин: 3–32 символа, латиница, цифры, _ . -");
        if ((req.Password ?? "").Length < 6)
            return Errors.Bad("Пароль не короче 6 символов");
        if (name.Length > 40)
            return Errors.Bad("Имя не длиннее 40 символов");
        if (name.Length == 0) name = login;

        string hash = BCrypt.Net.BCrypt.HashPassword(req.Password, 11);

        await using var conn = await db.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        long id;
        try
        {
            await using var cmd = new NpgsqlCommand(
                "insert into psypath.users (login, password_hash, display_name) values ($1, $2, $3) returning id", conn, tx);
            cmd.Parameters.AddWithValue(login);
            cmd.Parameters.AddWithValue(hash);
            cmd.Parameters.AddWithValue(name);
            id = (long)(await cmd.ExecuteScalarAsync())!;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Errors.Conflict("Такой логин уже занят");
        }

        await using (var cmd = new NpgsqlCommand("insert into psypath.progress (user_id) values ($1)", conn, tx))
        {
            cmd.Parameters.AddWithValue(id);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();

        return Results.Ok(new AuthResponse(tokens.Create(id, "user"), new UserDto(id, login, name, "user")));
    }

    static async Task<IResult> Login(AuthRequest req, NpgsqlDataSource db, Tokens tokens)
    {
        await using var cmd = db.CreateCommand(
            "select id, login, password_hash, display_name, role, is_blocked from psypath.users where lower(login) = lower($1)");
        cmd.Parameters.AddWithValue((req.Login ?? "").Trim());
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync() || !BCrypt.Net.BCrypt.Verify(req.Password ?? "", r.GetString(2)))
            return Errors.Unauthorized("Неверный логин или пароль");
        if (r.GetBoolean(5))
            return Errors.Forbidden("Аккаунт заблокирован");

        var user = new UserDto(r.GetInt64(0), r.GetString(1), r.GetString(3), r.GetString(4));
        await r.DisposeAsync();

        await using var upd = db.CreateCommand("update psypath.users set last_login_at = now() where id = $1");
        upd.Parameters.AddWithValue(user.Id);
        await upd.ExecuteNonQueryAsync();

        return Results.Ok(new AuthResponse(tokens.Create(user.Id, user.Role), user));
    }
}

// ошибки в одном виде: { "error": "текст для пользователя" }
public static class Errors
{
    public static IResult Bad(string msg) => Results.Json(new { error = msg }, statusCode: 400);
    public static IResult Unauthorized(string msg) => Results.Json(new { error = msg }, statusCode: 401);
    public static IResult Forbidden(string msg) => Results.Json(new { error = msg }, statusCode: 403);
    public static IResult NotFound(string msg) => Results.Json(new { error = msg }, statusCode: 404);
    public static IResult Conflict(string msg) => Results.Json(new { error = msg }, statusCode: 409);
}
