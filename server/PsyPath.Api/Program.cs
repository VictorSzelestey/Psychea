using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PsyPath.Api;

var builder = WebApplication.CreateBuilder(args);

var connString = builder.Configuration.GetConnectionString("Db");
if (string.IsNullOrWhiteSpace(connString))
    throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Db (appsettings.Development.json или переменная ConnectionStrings__Db)");

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key должен быть не короче 32 символов");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connString));
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<Tokens>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // claims остаются как в токене: "sub" и "role"
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Issuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("admin", p => p.RequireRole("admin"));

// не больше 10 попыток входа и регистрации в минуту с одного адреса
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", async (NpgsqlDataSource db) =>
{
    await using var cmd = db.CreateCommand("select max(version) from psypath.schema_version");
    return Results.Ok(new { status = "ok", schema = await cmd.ExecuteScalarAsync() });
});

app.MapAuth();
app.MapMe();
app.MapContent();
app.MapAdmin();

app.Run();
