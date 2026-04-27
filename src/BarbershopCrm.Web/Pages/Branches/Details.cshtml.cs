using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Branches;

/// <summary>
/// Публичная страница филиала. Показывает состав мастеров, доступные в
/// филиале услуги (пересечение каталога и компетенций мастеров) и ближайшее
/// свободное окно по каждому мастеру на горизонте двух недель.
/// </summary>
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISlotService _slots;

    public DetailsModel(ApplicationDbContext db, ISlotService slots)
    {
        _db = db;
        _slots = slots;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Branch? Branch { get; private set; }
    public IReadOnlyList<MasterCard> Masters { get; private set; } = Array.Empty<MasterCard>();
    public IReadOnlyList<Domain.Entities.Service> Services { get; private set; }
        = Array.Empty<Domain.Entities.Service>();
    public (DateOnly Date, TimeOnly Time)? BranchNextSlot { get; private set; }

    public record MasterCard(Master Master, (DateOnly Date, TimeOnly Time)? NextSlot);

    public async Task<IActionResult> OnGetAsync()
    {
        Branch = await _db.Branches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BranchId == Id);
        if (Branch is null) return RedirectToPage("/Booking/Index");

        var masters = await _db.Masters.AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.IsActive
                && _db.MasterBranches.Any(mb => mb.MasterId == m.MasterId && mb.BranchId == Id))
            .OrderBy(m => m.Persona.LastName)
            .ToListAsync();

        Services = await _db.Services.AsNoTracking()
            .Where(s => s.IsActive
                && _db.MasterServices.Any(ms => ms.ServiceId == s.ServiceId
                    && _db.MasterBranches.Any(mb => mb.MasterId == ms.MasterId && mb.BranchId == Id)))
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
            .ToListAsync();

        // Ближайшее окно у каждого мастера. Берём самую быстрое/массовое
        // услугу из прайса, чтобы оценка «первого свободного» была
        // консервативной (короткие услуги чаще помещаются в расписание).
        var sampleService = Services.OrderBy(s => s.DurationMinutes).FirstOrDefault();

        var cards = new List<MasterCard>(masters.Count);
        foreach (var m in masters)
        {
            (DateOnly, TimeOnly)? next = null;
            if (sampleService is not null
                && await _db.MasterServices.AnyAsync(ms => ms.MasterId == m.MasterId && ms.ServiceId == sampleService.ServiceId))
            {
                next = await _slots.GetNextAvailableSlotAsync(m.MasterId, Id, sampleService.ServiceId, horizonDays: 14);
            }
            cards.Add(new MasterCard(m, next));
        }
        Masters = cards;

        BranchNextSlot = cards
            .Select(c => c.NextSlot)
            .Where(s => s.HasValue)
            .OrderBy(s => s!.Value.Item1).ThenBy(s => s!.Value.Item2)
            .FirstOrDefault();

        return Page();
    }
}
