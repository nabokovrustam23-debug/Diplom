namespace BarbershopCrm.Infrastructure.Services;

/// <summary>
/// Сервис расчёта свободных временных слотов для записи клиента.
/// </summary>
public interface ISlotService
{
    /// <summary>
    /// Возвращает список свободных времён начала записи для пары
    /// (мастер, филиал, услуга) на указанную дату с учётом рабочего расписания
    /// мастера, обедов, выходных и существующих записей.
    /// </summary>
    Task<IReadOnlyList<TimeOnly>> GetAvailableSlotsAsync(
        int masterId,
        int branchId,
        int serviceId,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
