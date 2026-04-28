namespace BarbershopCrm.Domain.Entities;

/// <summary>
/// Минимальный аудит действий сотрудников: кто, когда и что изменил.
/// Используется для расследования ситуаций «кто перенёс запись», «кто
/// сбросил пароль», «кто пригласил пользователя». Не предназначен для
/// массивной телеметрии — только для критичных пользовательских действий.
/// </summary>
public class AuditLog
{
    public int AuditLogId { get; set; }

    /// <summary>Email актёра в момент действия (на случай удаления учётки).</summary>
    public string ActorEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
