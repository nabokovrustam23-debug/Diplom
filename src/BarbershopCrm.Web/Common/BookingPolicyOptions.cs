namespace BarbershopCrm.Web.Common;

/// <summary>
/// Параметры политики бронирования. Подгружаются из секции "BookingPolicy"
/// в appsettings.json. Ключевые значения подбираются под конкретную сеть.
/// </summary>
public class BookingPolicyOptions
{
    public const string SectionName = "BookingPolicy";

    /// <summary>Минимальный интервал до начала записи, при котором клиент
    /// ещё может её отменить или перенести самостоятельно (часы).</summary>
    public int MinHoursBeforeCancel { get; set; } = 2;

    /// <summary>Буфер между записями одного мастера в минутах (на уборку,
    /// перерыв, переключение). Учитывается при подборе свободных слотов.</summary>
    public int BufferMinutes { get; set; } = 0;

    /// <summary>Горизонт онлайн-записи в днях (на сколько вперёд показывать слоты).</summary>
    public int HorizonDays { get; set; } = 14;
}
