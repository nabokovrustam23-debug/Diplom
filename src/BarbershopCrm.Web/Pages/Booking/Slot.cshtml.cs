using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

public class SlotModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISlotService _slots;

    public SlotModel(ApplicationDbContext db, ISlotService slots)
    {
        _db = db;
        _slots = slots;
    }

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ServiceId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int MasterId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Date { get; set; }

    /// <summary>Быстрый фильтр: «earliest» (ближайшее окно), «evening» (только вечер), «weekend» (только выходные).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Quick { get; set; }

    public Branch? Branch { get; private set; }
    public Domain.Entities.Service? Service { get; private set; }
    public Domain.Entities.Master? Master { get; private set; }
    public bool IsAnyMaster => MasterId == 0;

    public IReadOnlyList<DateOnly> AvailableDates { get; private set; } = Array.Empty<DateOnly>();

    /// <summary>
    /// Список (Время, MasterId). Для конкретного мастера MasterId совпадает у всех элементов.
    /// Для режима «Любой мастер» — у каждого слота свой мастер.
    /// </summary>
    public IReadOnlyList<(TimeOnly Time, int MasterId)> AvailableSlots { get; private set; }
        = Array.Empty<(TimeOnly, int)>();

    public IReadOnlyList<(TimeOnly Time, int MasterId)> SlotsMorning =>
        AvailableSlots.Where(s => s.Time < new TimeOnly(12, 0)).ToList();
    public IReadOnlyList<(TimeOnly Time, int MasterId)> SlotsDay =>
        AvailableSlots.Where(s => s.Time >= new TimeOnly(12, 0) && s.Time < new TimeOnly(17, 0)).ToList();
    public IReadOnlyList<(TimeOnly Time, int MasterId)> SlotsEvening =>
        AvailableSlots.Where(s => s.Time >= new TimeOnly(17, 0)).ToList();

    public async Task<IActionResult> OnGetAsync()
    {
        Branch = await _db.Branches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BranchId == BranchId);
        Service = await _db.Services.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceId == ServiceId);

        if (Branch is null || Service is null)
        {
            return RedirectToPage("Index");
        }

        if (!IsAnyMaster)
        {
            Master = await _db.Masters.AsNoTracking()
                .Include(m => m.Persona)
                .FirstOrDefaultAsync(m => m.MasterId == MasterId);

            if (Master is null)
            {
                return RedirectToPage("Index");
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var allDates = Enumerable.Range(0, 14).Select(i => today.AddDays(i)).ToList();

        // Фильтр «Выходные» — оставляем в подборе дат только суб/вс.
        AvailableDates = Quick == "weekend"
            ? allDates.Where(d => d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday).ToList()
            : allDates;

        if (AvailableDates.Count == 0) AvailableDates = allDates;

        // Для «earliest»: ищем ближайший день, в котором есть хотя бы один слот.
        if (Quick == "earliest" && Date is null)
        {
            foreach (var d in AvailableDates)
            {
                var candidate = IsAnyMaster
                    ? await _slots.GetAvailableSlotsForAnyMasterAsync(BranchId, ServiceId, d)
                    : (await _slots.GetAvailableSlotsAsync(MasterId, BranchId, ServiceId, d))
                        .Select(t => (t, MasterId)).ToList();
                if (candidate.Count > 0)
                {
                    Date = d;
                    AvailableSlots = candidate.Take(1).ToList();
                    return Page();
                }
            }
        }

        Date ??= AvailableDates[0];

        if (IsAnyMaster)
        {
            AvailableSlots = await _slots.GetAvailableSlotsForAnyMasterAsync(BranchId, ServiceId, Date.Value);
        }
        else
        {
            var slots = await _slots.GetAvailableSlotsAsync(MasterId, BranchId, ServiceId, Date.Value);
            AvailableSlots = slots.Select(t => (t, MasterId)).ToList();
        }

        // Фильтр «Вечером» — оставляем только слоты >= 17:00.
        if (Quick == "evening")
        {
            AvailableSlots = AvailableSlots.Where(s => s.Time >= new TimeOnly(17, 0)).ToList();
        }

        return Page();
    }
}
