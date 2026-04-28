using BarbershopCrm.Domain.Enums;

namespace BarbershopCrm.Domain.Entities;

public class Service
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Категория позиции каталога. По умолчанию — «Прочее»; задаёт быстрый
    /// фильтр на публичной странице записи и группировку в админ-списке.
    /// </summary>
    public ServiceCategory Category { get; set; } = ServiceCategory.Other;

    /// <summary>
    /// Признак доступности услуги к записи. Неактивные услуги скрываются
    /// из публичного каталога и из шага выбора услуги, но не удаляются из
    /// базы (история записей остаётся целой).
    /// </summary>
    public bool IsActive { get; set; } = true;

    public ICollection<MasterService> MasterServices { get; set; } = new List<MasterService>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
