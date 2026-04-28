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

    public IReadOnlyList<DateOnly> AvailableDates { get; private set; } = Array.Empty<DateOnly>();
    public IReadOnlyList<TimeOnly> AvailableSlots { get; private set; } = Array.Empty<TimeOnly>();

    public async Task<IActionResult> OnGetAsync()
    {
        Branch = await _db.Branches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BranchId == BranchId);
        Service = await _db.Services.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceId == ServiceId);
        Master = await _db.Masters.AsNoTracking()
            .Include(m => m.Persona)
            .FirstOrDefaultAsync(m => m.MasterId == MasterId);

        if (Branch is null || Service is null || Master is null)
        {
            return RedirectToPage("Index");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        AvailableDates = Enumerable.Range(0, 7).Select(i => today.AddDays(i)).ToList();
        Date ??= AvailableDates[0];

        AvailableSlots = await _slots.GetAvailableSlotsAsync(MasterId, BranchId, ServiceId, Date.Value);

        return Page();
    }
}
