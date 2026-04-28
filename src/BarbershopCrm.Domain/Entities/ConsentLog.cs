namespace BarbershopCrm.Domain.Entities;

/// <summary>
/// Журнал согласий на обработку персональных данных. Фиксирует факт
/// и контекст явного согласия (152-ФЗ): какой клиент, какая версия
/// политики, IP/User-Agent на момент согласия.
/// </summary>
public class ConsentLog
{
    public int ConsentLogId { get; set; }
    public int? PersonaId { get; set; }
    /// <summary>Если согласие даёт гость — телефон вместо PersonaId.</summary>
    public string? GuestPhone { get; set; }
    /// <summary>Версия политики, на которую дано согласие.</summary>
    public string PolicyVersion { get; set; } = "1.0";
    public DateTime GivenAtUtc { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public Persona? Persona { get; set; }
}
