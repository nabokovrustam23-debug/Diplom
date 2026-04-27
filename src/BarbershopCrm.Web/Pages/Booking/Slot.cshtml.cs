using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

public class SlotModel : PageModel
{
    /// <summary>Горизонт подбора дат в днях.</summary>
    public const int HorizonDays = 14;

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

    /// <summary>Быстрые фильтры (могут комбинироваться): earliest, morning, day, evening, weekend.</summary>
    [BindProperty(SupportsGet = true, Name = "quick")]
    public string[] Quick { get; set; } = Array.Empty<string>();

    public bool HasFilter(string key) => Quick.Contains(key, StringComparer.OrdinalIgnoreCase);
    public bool FilterWeekend => HasFilter("weekend");
    public bool FilterEarliest => HasFilter("earliest");
    public bool FilterMorning => HasFilter("morning");
    public bool FilterDay => HasFilter("day");
    public bool FilterEvening => HasFilter("evening");

    public Branch? Branch { get; private set; }
    public Domain.Entities.Service? Service { get; private set; }
    public Domain.Entities.Master? Master { get; private set; }
    public bool IsAnyMaster => MasterId == 0;

    public IReadOnlyList<DateOnly> AvailableDates { get; private set; } = Array.Empty<DateOnly>();

    /// <summary>
    /// Список (Время, MasterId, IsPast). Для конкретного мастера MasterId совпадает у всех элементов.
    /// IsPast = true для слотов, которые уже прошли в активном дне (используется для визуального затенения).
    /// </summary>
    public IReadOnlyList<SlotView> AvailableSlots { get; private set; } = Array.Empty<SlotView>();

    public record SlotView(TimeOnly Time, int MasterId, bool IsPast);

    public IReadOnlyList<SlotView> SlotsMorning =>
        AvailableSlots.Where(s => s.Time < new TimeOnly(12, 0)).ToList();
    public IReadOnlyList<SlotView> SlotsDay =>
        AvailableSlots.Where(s => s.Time >= new TimeOnly(12, 0) && s.Time < new TimeOnly(17, 0)).ToList();
    public IReadOnlyList<SlotView> SlotsEvening =>
        AvailableSlots.Where(s => s.Time >= new TimeOnly(17, 0)).ToList();

    /// <summary>Подсказка, когда все слоты отфильтрованы или нет окон на выбранный день.</summary>
    public string? EmptyHint { get; private set; }

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
        var allDates = Enumerable.Range(0, HorizonDays).Select(i => today.AddDays(i)).ToList();

        AvailableDates = FilterWeekend
            ? allDates.Where(d => d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday).ToList()
            : allDates;

        if (AvailableDates.Count == 0) AvailableDates = allDates;

        // Для «earliest»: ищем ближайший день, в котором есть хотя бы один слот,
        // который одновременно удовлетворяет остальным фильтрам (утро/день/вечер).
        if (FilterEarliest && Date is null)
        {
            foreach (var d in AvailableDates)
            {
                var candidate = await LoadSlotsAsync(d);
                candidate = ApplyPartFilter(candidate);
                candidate = DropPastIfToday(candidate, d, today);
                if (candidate.Count > 0)
                {
                    Date = d;
                    AvailableSlots = candidate.Take(1).ToList();
                    return Page();
                }
            }
        }

        Date ??= AvailableDates[0];

        var slots = await LoadSlotsAsync(Date.Value);
        slots = ApplyPartFilter(slots);
        slots = DropPastIfToday(slots, Date.Value, today);
        AvailableSlots = slots;

        if (AvailableSlots.Count == 0)
        {
            EmptyHint = BuildEmptyHint();
        }

        return Page();
    }

    private async Task<List<SlotView>> LoadSlotsAsync(DateOnly d)
    {
        if (IsAnyMaster)
        {
            var raw = await _slots.GetAvailableSlotsForAnyMasterAsync(BranchId, ServiceId, d);
            return raw.Select(r => new SlotView(r.Time, r.MasterId, false)).ToList();
        }
        var single = await _slots.GetAvailableSlotsAsync(MasterId, BranchId, ServiceId, d);
        return single.Select(t => new SlotView(t, MasterId, false)).ToList();
    }

    private List<SlotView> ApplyPartFilter(List<SlotView> slots)
    {
        // Утро/день/вечер работают как OR: если включён хотя бы один — оставляем только
        // соответствующие части суток. Если не включено ни одного — оставляем всё.
        if (!FilterMorning && !FilterDay && !FilterEvening) return slots;
        bool Match(TimeOnly t) =>
            (FilterMorning && t < new TimeOnly(12, 0))
            || (FilterDay && t >= new TimeOnly(12, 0) && t < new TimeOnly(17, 0))
            || (FilterEvening && t >= new TimeOnly(17, 0));
        return slots.Where(s => Match(s.Time)).ToList();
    }

    /// <summary>Для сегодняшнего дня помечаем прошедшие слоты как IsPast=true, не удаляя их
    /// из списка сразу (в шаблоне они отрисуются затенёнными и некликабельными).</summary>
    private List<SlotView> DropPastIfToday(List<SlotView> slots, DateOnly d, DateOnly today)
    {
        if (d != today) return slots;
        var now = TimeOnly.FromDateTime(DateTime.Now);
        return slots.Select(s => s with { IsPast = s.Time <= now }).Where(s => !s.IsPast).ToList();
    }

    private string BuildEmptyHint()
    {
        if (FilterMorning || FilterDay || FilterEvening || FilterWeekend)
        {
            return "На этот день нет слотов под выбранные фильтры. Попробуйте другое время суток или снимите фильтры.";
        }
        return IsAnyMaster
            ? "На выбранный день все слоты заняты. Выберите другой день."
            : "У этого мастера на выбранный день нет свободных окон. Попробуйте выбрать «Любой мастер».";
    }
}
