using Npgsql;

namespace PsyPath.Api;

public static class AdminEndpoints
{
    public static void MapAdmin(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization("admin");
        g.MapGet("/stats", Stats);
        g.MapGet("/users", Users);
        g.MapGet("/users/{id:long}", UserDetails);
        g.MapContentAdmin();
    }

    static async Task<IResult> Stats(NpgsqlDataSource db)
    {
        object overview;
        await using (var cmd = db.CreateCommand(
            "select users_total, active_7d, avg_rating, lessons_finished, questionnaires_taken from psypath.v_stats_overview"))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            await r.ReadAsync();
            overview = new
            {
                usersTotal = r.GetInt64(0),
                active7d = r.GetInt64(1),
                avgRating = r.IsDBNull(2) ? (decimal?)null : r.GetDecimal(2),
                lessonsFinished = r.GetInt64(3),
                questionnairesTaken = r.GetInt64(4)
            };
        }

        var lessons = new List<object>();
        await using (var cmd = db.CreateCommand(
            "select lesson_id, attempts, users, avg_accuracy from psypath.v_lesson_stats order by attempts desc"))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync())
                lessons.Add(new { lessonId = r.GetString(0), attempts = r.GetInt64(1), users = r.GetInt64(2), avgAccuracy = r.GetDecimal(3) });

        var questionnaires = new List<object>();
        await using (var cmd = db.CreateCommand(
            "select questionnaire_id, headline, times from psypath.v_questionnaire_stats order by questionnaire_id, times desc"))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync())
                questionnaires.Add(new { questionnaireId = r.GetString(0), headline = r.GetString(1), times = r.GetInt64(2) });

        return Results.Ok(new { overview, lessons, questionnaires });
    }

    static async Task<IResult> Users(NpgsqlDataSource db)
    {
        await using var cmd = db.CreateCommand("""
            select u.id, u.login, u.display_name, u.role, u.created_at, u.last_login_at,
                   p.rating, p.total_xp, p.streak, p.last_active, p.answered, p.correct,
                   (select count(*) from psypath.lesson_results l where l.user_id = u.id)
            from psypath.users u join psypath.progress p on p.user_id = u.id
            order by u.created_at desc
            limit 500
            """);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<object>();
        while (await r.ReadAsync())
        {
            int answered = r.GetInt32(10);
            list.Add(new
            {
                id = r.GetInt64(0),
                login = r.GetString(1),
                displayName = r.GetString(2),
                role = r.GetString(3),
                createdAt = r.GetDateTime(4),
                lastLoginAt = r.IsDBNull(5) ? (DateTime?)null : r.GetDateTime(5),
                rating = Math.Round(r.GetDouble(6)),
                totalXp = r.GetInt32(7),
                streak = r.GetInt32(8),
                lastActive = r.IsDBNull(9) ? null : r.GetFieldValue<DateOnly>(9).ToString("yyyy-MM-dd"),
                accuracy = answered == 0 ? 0 : (int)Math.Round(r.GetInt32(11) * 100.0 / answered),
                lessonsFinished = r.GetInt64(12)
            });
        }
        return Results.Ok(list);
    }

    // один пользователь: профиль, прогресс, все опросники с баллами по шкалам и пройденные уроки
    static async Task<IResult> UserDetails(long id, NpgsqlDataSource db)
    {
        object user;
        await using (var cmd = db.CreateCommand("""
            select u.login, u.display_name, u.role, u.created_at, u.last_login_at,
                   p.rating, p.total_xp, p.streak, p.last_active, p.answered, p.correct
            from psypath.users u join psypath.progress p on p.user_id = u.id
            where u.id = $1
            """))
        {
            cmd.Parameters.AddWithValue(id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Errors.NotFound("Пользователь не найден");
            int answered = r.GetInt32(9);
            user = new
            {
                id,
                login = r.GetString(0),
                displayName = r.GetString(1),
                role = r.GetString(2),
                createdAt = r.GetDateTime(3),
                lastLoginAt = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4),
                rating = Math.Round(r.GetDouble(5)),
                totalXp = r.GetInt32(6),
                streak = r.GetInt32(7),
                lastActive = r.IsDBNull(8) ? null : r.GetFieldValue<DateOnly>(8).ToString("yyyy-MM-dd"),
                answered,
                accuracy = answered == 0 ? 0 : (int)Math.Round(r.GetInt32(10) * 100.0 / answered)
            };
        }

        var questionnaires = new List<object>();
        await using (var cmd = db.CreateCommand("""
            select questionnaire_id, headline, scores::text, taken_at
            from psypath.questionnaire_results where user_id = $1 order by taken_at desc limit 200
            """))
        {
            cmd.Parameters.AddWithValue(id);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                using var scores = System.Text.Json.JsonDocument.Parse(r.GetString(2));
                questionnaires.Add(new
                {
                    questionnaireId = r.GetString(0),
                    headline = r.GetString(1),
                    scores = scores.RootElement.Clone(),
                    takenAt = r.GetDateTime(3)
                });
            }
        }

        var lessons = new List<object>();
        await using (var cmd = db.CreateCommand("""
            select lesson_id, accuracy, mistakes, xp, finished_at
            from psypath.lesson_results where user_id = $1 order by finished_at desc limit 200
            """))
        {
            cmd.Parameters.AddWithValue(id);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                lessons.Add(new
                {
                    lessonId = r.GetString(0),
                    accuracy = r.GetInt32(1),
                    mistakes = r.GetInt32(2),
                    xp = r.GetInt32(3),
                    finishedAt = r.GetDateTime(4)
                });
        }

        return Results.Ok(new { user, questionnaires, lessons });
    }
}
