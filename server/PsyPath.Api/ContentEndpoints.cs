using System.Security.Claims;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace PsyPath.Api;

public record ContentUpdateRequest(int Version, JsonElement Data);

public static class ContentEndpoints
{
    // имя документа → ключ, который обязан быть в корне JSON (иначе приложение его не прочитает)
    static readonly Dictionary<string, string> Required = new()
    {
        ["lessons"] = "sections",
        ["questionnaires"] = "items",
        ["rating"] = "levels",
        ["reference"] = "entries"
    };

    public static void MapContent(this WebApplication app)
    {
        // контент открыт всем: приложение грузит его и до входа
        app.MapGet("/api/content/versions", Versions);
        app.MapGet("/api/content/{name}", Get);
    }

    public static void MapContentAdmin(this RouteGroupBuilder admin)
    {
        admin.MapPut("/content/{name}", Update);
        admin.MapGet("/content/{name}/history", History);
        admin.MapPost("/content/{name}/rollback/{version:int}", Rollback);
    }

    static async Task<IResult> Versions(NpgsqlDataSource db)
    {
        await using var cmd = db.CreateCommand("select name, version from psypath.content");
        await using var r = await cmd.ExecuteReaderAsync();
        var result = new Dictionary<string, int>();
        while (await r.ReadAsync()) result[r.GetString(0)] = r.GetInt32(1);
        return Results.Ok(result);
    }

    static async Task<IResult> Get(string name, NpgsqlDataSource db)
    {
        if (!Required.ContainsKey(name)) return Errors.NotFound("Нет такого контента");
        await using var cmd = db.CreateCommand("select version, data::text, updated_at from psypath.content where name = $1");
        cmd.Parameters.AddWithValue(name);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return Errors.NotFound("Нет такого контента");
        using var data = JsonDocument.Parse(r.GetString(1));
        return Results.Ok(new { name, version = r.GetInt32(0), updatedAt = r.GetDateTime(2), data = data.RootElement.Clone() });
    }

    // version — та версия, которую админ открыл; если кто-то успел сохранить раньше, вернётся 409
    static async Task<IResult> Update(string name, ContentUpdateRequest req, ClaimsPrincipal me, NpgsqlDataSource db)
    {
        if (!Required.TryGetValue(name, out var key)) return Errors.NotFound("Нет такого контента");
        if (req.Data.ValueKind != JsonValueKind.Object || !HasArray(req.Data, key))
            return Errors.Bad($"В корне JSON нужен массив \"{key}\"");
        return await Save(db, name, req.Version, req.Data.GetRawText(), me.UserId());
    }

    static async Task<IResult> Rollback(string name, int version, ClaimsPrincipal me, NpgsqlDataSource db)
    {
        if (!Required.ContainsKey(name)) return Errors.NotFound("Нет такого контента");
        await using var cmd = db.CreateCommand("select data::text from psypath.content_history where name = $1 and version = $2");
        cmd.Parameters.AddWithValue(name);
        cmd.Parameters.AddWithValue(version);
        if (await cmd.ExecuteScalarAsync() is not string old) return Errors.NotFound("Нет такой версии");
        return await Save(db, name, null, old, me.UserId());
    }

    static async Task<IResult> History(string name, NpgsqlDataSource db)
    {
        await using var cmd = db.CreateCommand("""
            select h.version, h.changed_at, coalesce(u.login, '')
            from psypath.content_history h left join psypath.users u on u.id = h.changed_by
            where h.name = $1 order by h.version desc limit 50
            """);
        cmd.Parameters.AddWithValue(name);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<object>();
        while (await r.ReadAsync())
            list.Add(new { version = r.GetInt32(0), changedAt = r.GetDateTime(1), changedBy = r.GetString(2) });
        return Results.Ok(list);
    }

    // старая версия уходит в историю, новая получает version + 1
    static async Task<IResult> Save(NpgsqlDataSource db, string name, int? expectedVersion, string json, long userId)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        int current;
        await using (var sel = new NpgsqlCommand("select version from psypath.content where name = $1 for update", conn, tx))
        {
            sel.Parameters.AddWithValue(name);
            current = (int)(await sel.ExecuteScalarAsync())!;
        }
        if (expectedVersion != null && expectedVersion != current)
            return Errors.Conflict($"Контент уже изменили (версия {current}). Обнови и внеси правку заново.");

        await using (var hist = new NpgsqlCommand("""
            insert into psypath.content_history (name, version, data, changed_by)
            select name, version, data, $2 from psypath.content where name = $1
            """, conn, tx))
        {
            hist.Parameters.AddWithValue(name);
            hist.Parameters.AddWithValue(userId);
            await hist.ExecuteNonQueryAsync();
        }

        await using (var upd = new NpgsqlCommand("""
            update psypath.content set data = $2, version = version + 1, updated_at = now(), updated_by = $3
            where name = $1
            """, conn, tx))
        {
            upd.Parameters.AddWithValue(name);
            upd.Parameters.Add(new NpgsqlParameter { Value = json, NpgsqlDbType = NpgsqlDbType.Jsonb });
            upd.Parameters.AddWithValue(userId);
            await upd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        return Results.Ok(new { name, version = current + 1 });
    }

    static bool HasArray(JsonElement obj, string key)
    {
        foreach (var p in obj.EnumerateObject())
            if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
                return p.Value.ValueKind == JsonValueKind.Array;
        return false;
    }
}
