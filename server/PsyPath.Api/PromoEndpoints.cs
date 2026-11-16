using System.Security.Claims;
using Npgsql;

namespace PsyPath.Api;

public record PromoRedeemRequest(string? Code);
public record PromoCreateRequest(string? Code, string? Reward, int Amount, string? Item, int? MaxUses);

// промокоды (db/004_promo.sql): админ создаёт код с наградой, пользователь вводит его в магазине один раз.
// Сервер только проверяет код и запоминает активацию; награду начисляет приложение в свой прогресс
public static class PromoEndpoints
{
    const int MaxCrystals = 100_000;

    public static void MapPromo(this WebApplication app)
    {
        // подбор кодов перебором упирается в тот же лимит, что вход
        app.MapGroup("/api/me").RequireAuthorization().MapPost("/promo", Redeem).RequireRateLimiting("auth");

        var admin = app.MapGroup("/api/admin/promo").RequireAuthorization("admin");
        admin.MapGet("/", List);
        admin.MapPost("/", Create);
        admin.MapDelete("/{code}", Delete);
    }

    // регистр и пробелы по краям не важны: «  psy2026 » = «PSY2026»
    static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant();

    static bool Valid(string code) =>
        code.Length is >= 3 and <= 32 && code.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    static async Task<IResult> Redeem(PromoRedeemRequest req, ClaimsPrincipal me, NpgsqlDataSource db)
    {
        string code = Normalize(req.Code);
        if (!Valid(code)) return Errors.NotFound("Такого промокода нет");

        await using var conn = await db.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        string reward, item;
        int amount, uses;
        int? maxUses;
        await using (var cmd = new NpgsqlCommand(
            "select reward, amount, item, max_uses, uses from psypath.promo_codes where code = $1 for update", conn, tx))
        {
            cmd.Parameters.AddWithValue(code);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Errors.NotFound("Такого промокода нет");
            reward = r.GetString(0);
            amount = r.GetInt32(1);
            item = r.GetString(2);
            maxUses = r.IsDBNull(3) ? null : r.GetInt32(3);
            uses = r.GetInt32(4);
        }

        // без Commit транзакция откатывается: отказ ничего не записывает
        await using (var cmd = new NpgsqlCommand(
            "insert into psypath.promo_redemptions (code, user_id) values ($1, $2) on conflict do nothing", conn, tx))
        {
            cmd.Parameters.AddWithValue(code);
            cmd.Parameters.AddWithValue(me.UserId());
            if (await cmd.ExecuteNonQueryAsync() == 0) return Errors.Conflict("Этот промокод уже активирован на твоём аккаунте");
        }
        if (maxUses is int max && uses >= max) return Errors.Conflict("Промокод закончился");

        await using (var cmd = new NpgsqlCommand("update psypath.promo_codes set uses = uses + 1 where code = $1", conn, tx))
        {
            cmd.Parameters.AddWithValue(code);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        return Results.Ok(new { code, reward, amount, item });
    }

    static async Task<IResult> List(NpgsqlDataSource db)
    {
        await using var cmd = db.CreateCommand("""
            select code, reward, amount, item, max_uses, uses, created_at
            from psypath.promo_codes order by created_at desc
            """);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<object>();
        while (await r.ReadAsync())
            list.Add(new
            {
                code = r.GetString(0),
                reward = r.GetString(1),
                amount = r.GetInt32(2),
                item = r.GetString(3),
                maxUses = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
                uses = r.GetInt32(5),
                createdAt = r.GetDateTime(6)
            });
        return Results.Ok(list);
    }

    static async Task<IResult> Create(PromoCreateRequest req, ClaimsPrincipal me, NpgsqlDataSource db)
    {
        string code = Normalize(req.Code);
        if (!Valid(code)) return Errors.Bad("Код: от 3 до 32 символов, буквы, цифры, - и _");
        string item = (req.Item ?? "").Trim();
        switch (req.Reward)
        {
            case "crystals" when req.Amount is < 1 or > MaxCrystals:
                return Errors.Bad($"Кристаллов: от 1 до {MaxCrystals}");
            case "crystals":
                item = "";
                break;
            case "item" when item.Length is 0 or > 64:
                return Errors.Bad("Выбери предмет магазина");
            case "item":
                break;
            default:
                return Errors.Bad("Награда: crystals или item");
        }
        if (req.MaxUses is < 1) return Errors.Bad("Число активаций — от 1 или без ограничения");

        await using var cmd = db.CreateCommand("""
            insert into psypath.promo_codes (code, reward, amount, item, max_uses, created_by)
            values ($1, $2, $3, $4, $5, $6) on conflict do nothing
            """);
        cmd.Parameters.AddWithValue(code);
        cmd.Parameters.AddWithValue(req.Reward);
        cmd.Parameters.AddWithValue(req.Reward == "crystals" ? req.Amount : 0);
        cmd.Parameters.AddWithValue(item);
        cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)req.MaxUses ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
        cmd.Parameters.AddWithValue(me.UserId());
        if (await cmd.ExecuteNonQueryAsync() == 0) return Errors.Conflict("Такой промокод уже есть");
        return Results.Ok(new { code });
    }

    static async Task<IResult> Delete(string code, NpgsqlDataSource db)
    {
        await using var cmd = db.CreateCommand("delete from psypath.promo_codes where code = $1");
        cmd.Parameters.AddWithValue(Normalize(code));
        if (await cmd.ExecuteNonQueryAsync() == 0) return Errors.NotFound("Промокод не найден");
        return Results.Ok(new { deleted = true });
    }
}
