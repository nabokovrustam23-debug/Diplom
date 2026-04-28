namespace BarbershopCrm.Domain.Entities;

/// <summary>
/// Отзыв клиента о выполненной услуге. Привязан к конкретной записи
/// (одна запись — максимум один отзыв) и наследует от неё мастера/услугу/филиал
/// для агрегатов вида «средний рейтинг по мастеру/филиалу».
/// </summary>
public class Review
{
    public int ReviewId { get; set; }
    public int BookingId { get; set; }
    /// <summary>1..5.</summary>
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Скрытые отзывы не показываются публично, но не удаляются —
    /// для аудита и возможного восстановления.</summary>
    public bool IsHidden { get; set; }

    public Booking Booking { get; set; } = null!;
}
