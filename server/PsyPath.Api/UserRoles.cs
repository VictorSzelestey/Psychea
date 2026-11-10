using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Npgsql;

namespace PsyPath.Api;

public record AccountAccess(string Role, bool IsBlocked);
public enum ManagerChange { Updated, NotFound, Administrator }

public interface IUserRoles
{
    Task<AccountAccess?> Find(long id, CancellationToken cancel);
    Task<ManagerChange> SetManager(long id, bool manager, CancellationToken cancel);
}

public sealed class DbUserRoles(NpgsqlDataSource db) : IUserRoles
{
    public async Task<AccountAccess?> Find(long id, CancellationToken cancel)
    {
        await using var cmd = db.CreateCommand("select role, is_blocked from psypath.users where id = $1");
        cmd.Parameters.AddWithValue(id);
        await using var r = await cmd.ExecuteReaderAsync(cancel);
        return await r.ReadAsync(cancel) ? new AccountAccess(r.GetString(0), r.GetBoolean(1)) : null;
    }

    public async Task<ManagerChange> SetManager(long id, bool manager, CancellationToken cancel)
    {
        // Одним UPDATE: даже при одновременных запросах роль администратора не меняется.
        await using var cmd = db.CreateCommand("""
            update psypath.users set role = $2
            where id = $1 and role in ('user', 'manager') returning id
            """);
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(manager ? "manager" : "user");
        if (await cmd.ExecuteScalarAsync(cancel) != null) return ManagerChange.Updated;
        return await Find(id, cancel) == null ? ManagerChange.NotFound : ManagerChange.Administrator;
    }
}

public sealed record CurrentRoleRequirement(params string[] Roles) : IAuthorizationRequirement;

public sealed class CurrentRoleHandler(IUserRoles roles) : AuthorizationHandler<CurrentRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CurrentRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !long.TryParse(context.User.FindFirstValue("sub"), out long id)) return;
        var cancel = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        var account = await roles.Find(id, cancel);
        // Claims из старого JWT не сохраняют права после снятия роли или блокировки.
        if (account is { IsBlocked: false } && requirement.Roles.Contains(account.Role))
            context.Succeed(requirement);
    }
}

public static class RoleAuthorization
{
    public static IServiceCollection AddRoleAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IUserRoles, DbUserRoles>();
        services.AddSingleton<IAuthorizationHandler, CurrentRoleHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy("admin", p => p.RequireAuthenticatedUser().AddRequirements(new CurrentRoleRequirement("admin")))
            .AddPolicy("content", p => p.RequireAuthenticatedUser().AddRequirements(new CurrentRoleRequirement("admin", "manager")));
        return services;
    }
}
