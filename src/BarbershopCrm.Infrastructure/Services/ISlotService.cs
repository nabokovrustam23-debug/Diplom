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

    /// <summary>
    /// Для режима «Любой мастер»: возвращает свободные времена в указанный день
    /// и для каждого времени — id мастера, у которого этот слот свободен (если таких
    /// несколько, выбирается с минимальным id для стабильности).
    /// </summary>
    Task<IReadOnlyList<(TimeOnly Time, int MasterId)>> GetAvailableSlotsForAnyMasterAsync(
        int branchId,
        int serviceId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Для подсказки на шаге выбора мастера: ближайшее свободное окно у мастера
    /// в данном филиале для данной услуги в горизонте N дней. Возвращает null,
    /// если в горизонте свободных окон нет.
    /// </summary>
    Task<(DateOnly Date, TimeOnly Time)?> GetNextAvailableSlotAsync(
        int masterId,
        int branchId,
        int serviceId,
        int horizonDays,
        CancellationToken cancellationToken = default);
}
