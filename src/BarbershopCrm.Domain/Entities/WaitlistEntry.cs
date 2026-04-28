namespace BarbershopCrm.Domain.Entities;

/// <summary>
/// «Лист ожидания»: клиент хочет к конкретному мастеру/в филиал,
/// но свободных слотов в нужном окне нет. При появлении слота
/// администратор может связаться с клиентами из этого списка.
/// MasterId опционален — если null, клиент готов к любому мастеру филиала.
/// </summary>
public class WaitlistEntry
{
    public int WaitlistEntryId { get; set; }
    public int? PersonaId { get; set; }
    /// <summary>На случай, если ждёт гость без аккаунта.</summary>
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }

    public int BranchId { get; set; }
    public int ServiceId { get; set; }
    public int? MasterId { get; set; }

    /// <summary>Желательное окно (включительно).</summary>
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Когда администратор уведомил клиента о появившемся слоте.</summary>
    public DateTime? NotifiedAtUtc { get; set; }
    /// <summary>Закрытые записи (клиент или сам записался, или отказался).</summary>
    public bool IsClosed { get; set; }

    public Persona? Persona { get; set; }
    public Branch Branch { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public Master? Master { get; set; }
}
