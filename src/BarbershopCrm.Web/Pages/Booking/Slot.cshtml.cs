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

    public Branch? Branch { get; private set; }
    public Domain.Entities.Service? Service { get; private set; }
    public Master? Master { get; private set; }
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
        AvailableDates = Enumerable.Range(0, 7).Select(i => today.AddDays(i)).ToList();
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

        return Page();
    }
}
