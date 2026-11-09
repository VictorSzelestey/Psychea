using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace PsyPath.Api;

public record LessonResultRequest(string LessonId, int Accuracy, int Mistakes, int Xp);
public record QuestionnaireResultRequest(string QuestionnaireId, string Headline, JsonElement Scores);
public record DeleteRequest(string Password);

public static class MeEndpoints
{
    public static void MapMe(this WebApplication app)
    {
        var g = app.MapGroup("/api/me").RequireAuthorization();
        g.MapGet("/", Get);
        g.MapPut("/progress", PutProgress);
        g.MapPost("/lessons", PostLesson);
        g.MapPost("/questionnaires", PostQuestionnaire);
        // пароль проверяется, поэтому тот же лимит попыток, что у входа
        g.MapPost("/delete", DeleteAccount).RequireRateLimiting("auth");
    }

    // удаление аккаунта: профиль, прогресс и результаты (каскадом); правки контента остаются без автора
    static async Task<IResult> DeleteAccount(DeleteRequest req, ClaimsPrincipal me, NpgsqlDataSource db)
    {
        long id = me.UserId();
        string hash, role;
        await using (var cmd = db.CreateCommand("select password_hash, role from psypath.users where id = $1"))
        {
            cmd.Parameters.AddWithValue(id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Errors.NotFound("Пользователь не найден");
            hash = r.GetString(0);
            role = r.GetString(1);
        }
        if (!BCrypt.Net.BCrypt.Verify(req.Password ?? "", hash))
            return Errors.Forbidden("Неверный пароль");

        if (role == "admin")
        {
            await using var cnt = db.CreateCommand("select count(*) from psypath.users where role = 'admin'");
            if ((long)(await cnt.ExecuteScalarAsync())! <= 1)
                return Errors.Conflict("Это единственный админ. Сначала назначь другого админа.");
        }

        await using var del = db.CreateCommand("delete from psypath.users where id = $1");
        del.Parameters.AddWithValue(id);
        await del.ExecuteNonQueryAsync();
        return Results.Ok(new { deleted = true });
    }

    // профиль и прогресс; progress — тот же ProgressData, что приложение хранит у себя
    static async Task<IResult> Get(ClaimsPrincipal me, NpgsqlDataSource db)
    {
        await using var cmd = db.CreateCommand("""
            select u.id, u.login, u.display_name, u.role, p.data::text, p.updated_at
            from psypath.users u join psypath.progress p on p.user_id = u.id
            where u.id = $1
            """);
        cmd.Parameters.AddWithValue(me.UserId());
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return Errors.NotFound("Пользователь не найден");

        var user = new UserDto(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3));
        using var data = JsonDocument.Parse(r.GetString(4));
        return Results.Ok(new { user, progress = data.RootElement.Clone(), updatedAt = r.GetDateTime(5) });
    }

    // приложение присылает ProgressData целиком; числа для статистики достаём отсюда же
    static async Task<IResult> PutProgress(JsonElement body, ClaimsPrincipal me, NpgsqlDataSource db)
    {
        if (body.ValueKind != JsonValueKind.Object) return Errors.Bad("Ожидался объект прогресса");
        if (body.GetRawText().Length > 200_000) return Errors.Bad("Слишком большой прогресс");

        DateOnly? lastActive = DateOnly.TryParseExact(Str(body, "LastActive"), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        await using var cmd = db.CreateCommand("""
            update psypath.progress
            set rating = $2, total_xp = $3, streak = $4, last_active = $5,
                answered = $6, correct = $7, data = $8, updated_at = now()
            where user_id = $1
            """);
        cmd.Parameters.AddWithValue(me.UserId());
        cmd.Parameters.AddWithValue(Math.Clamp(Num(body, "Rating", 100), 0, 1000));
        cmd.Parameters.AddWithValue((int)Num(body, "TotalXp"));
        cmd.Parameters.AddWithValue((int)Num(body, "Streak"));
        cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)lastActive ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Date });
        cmd.Parameters.AddWithValue((int)Num(body, "Answered"));
        cmd.Parameters.AddWithValue((int)Num(body, "Correct"));
        cmd.Parameters.Add(new NpgsqlParameter { Value = body.GetRawText(), NpgsqlDbType = NpgsqlDbType.Jsonb });
        await cmd.ExecuteNonQueryAsync();
        return Results.Ok(new { saved = true });
    }

    static async Task<IResult> PostLesson(LessonResultRequest req, ClaimsPrincipal me, NpgsqlDataSource db)
    {
        if (string.IsNullOrWhiteSpace(req.LessonId) || req.LessonId.Length > 64) return Errors.Bad("Неверный lessonId");
        if (req.Accuracy is < 0 or > 100) return Errors.Bad("accuracy от 0 до 100");

        await using var cmd = db.CreateCommand(
            "insert into psypath.lesson_results (user_id, lesson_id, accuracy, mistakes, xp) values ($1, $2, $3, $4, $5)");
        cmd.Parameters.AddWithValue(me.UserId());
        cmd.Parameters.AddWithValue(req.LessonId);
        cmd.Parameters.AddWithValue(req.Accuracy);
        cmd.Parameters.AddWithValue(Math.Max(0, req.Mistakes));
        cmd.Parameters.AddWithValue(Math.Max(0, req.Xp));
        await cmd.ExecuteNonQueryAsync();
        return Results.Ok(new { saved = true });
    }

    static async Task<IResult> PostQuestionnaire(QuestionnaireResultRequest req, ClaimsPrincipal me, NpgsqlDataSource db)
    {
        if (string.IsNullOrWhiteSpace(req.QuestionnaireId) || req.QuestionnaireId.Length > 64) return Errors.Bad("Неверный questionnaireId");
        if (req.Scores.ValueKind != JsonValueKind.Array) return Errors.Bad("scores должен быть массивом");

        await using var cmd = db.CreateCommand(
            "insert into psypath.questionnaire_results (user_id, questionnaire_id, headline, scores) values ($1, $2, $3, $4)");
        cmd.Parameters.AddWithValue(me.UserId());
        cmd.Parameters.AddWithValue(req.QuestionnaireId);
        cmd.Parameters.AddWithValue((req.Headline ?? "").Trim());
        cmd.Parameters.Add(new NpgsqlParameter { Value = req.Scores.GetRawText(), NpgsqlDbType = NpgsqlDbType.Jsonb });
        await cmd.ExecuteNonQueryAsync();
        return Results.Ok(new { saved = true });
    }

    static JsonElement? Prop(JsonElement obj, string name)
    {
        foreach (var p in obj.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p.Value;
        return null;
    }

    static double Num(JsonElement obj, string name, double fallback = 0) =>
        Prop(obj, name) is { ValueKind: JsonValueKind.Number } v ? v.GetDouble() : fallback;

    static string Str(JsonElement obj, string name) =>
        Prop(obj, name) is { ValueKind: JsonValueKind.String } v ? v.GetString()! : "";
}
