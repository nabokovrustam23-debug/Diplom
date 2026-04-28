using System.Security.Claims;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;

namespace BarbershopCrm.Web.Common;

/// <summary>
/// Тонкая обёртка над <see cref="AuditLog"/> для записи критичных действий
/// сотрудников. Не вызывает SaveChanges — это делает вызывающий код вместе
/// со своими изменениями, чтобы запись в журнал и сама операция фиксировались
/// в одной транзакции SQLite.
/// </summary>
public static class AuditLogger
{
    public static void Log(ApplicationDbContext db, ClaimsPrincipal? actor, string action, string entityType, string entityId, string? details = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorEmail = actor?.FindFirstValue(ClaimTypes.Email) ?? actor?.Identity?.Name ?? "system",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            AtUtc = DateTime.UtcNow,
        });
    }
}
