using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

public class MasterModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISlotService _slots;

    public MasterModel(ApplicationDbContext db, ISlotService slots)
    {
        _db = db;
        _slots = slots;
    }

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ServiceId { get; set; }

    public Branch? Branch { get; private set; }
    public Domain.Entities.Service? Service { get; private set; }
    public IReadOnlyList<MasterCard> Masters { get; private set; } = Array.Empty<MasterCard>();
    public (DateOnly Date, TimeOnly Time)? AnyMasterNextSlot { get; private set; }

    public record MasterCard(Master Master, (DateOnly Date, TimeOnly Time)? NextSlot);

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

        var masters = await _db.Masters
            .AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.IsActive
                && _db.MasterBranches.Any(mb => mb.MasterId == m.MasterId && mb.BranchId == BranchId)
                && _db.MasterServices.Any(ms => ms.MasterId == m.MasterId && ms.ServiceId == ServiceId))
            .OrderBy(m => m.Persona.LastName)
            .ToListAsync();

        var cards = new List<MasterCard>(masters.Count);
        foreach (var m in masters)
        {
            var next = await _slots.GetNextAvailableSlotAsync(m.MasterId, BranchId, ServiceId, horizonDays: 14);
            cards.Add(new MasterCard(m, next));
        }
        Masters = cards;

        // «Любой мастер» — показываем самый ранний слот среди всех мастеров филиала.
        AnyMasterNextSlot = cards
            .Select(c => c.NextSlot)
            .Where(s => s.HasValue)
            .OrderBy(s => s!.Value.Date).ThenBy(s => s!.Value.Time)
            .FirstOrDefault();

        return Page();
    }
}
