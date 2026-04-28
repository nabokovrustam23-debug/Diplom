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

    /// <summary>Карточка мастера для шага выбора. Помимо ближайшего свободного
    /// слота, содержит средний рейтинг и количество отзывов — это помогает
    /// первичным клиентам сделать выбор без общения с администратором.</summary>
    public record MasterCard(Domain.Entities.Master Master, (DateOnly Date, TimeOnly Time)? NextSlot,
        double? AvgRating, int ReviewsCount);

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

        // Среднюю оценку считаем одним SQL-запросом по всем релевантным мастерам:
        // безопаснее, чем N+1 в цикле.
        var masterIds = masters.Select(m => m.MasterId).ToList();
        var ratings = await _db.Reviews.AsNoTracking()
            .Where(r => !r.IsHidden && masterIds.Contains(r.Booking.MasterId))
            .GroupBy(r => r.Booking.MasterId)
            .Select(g => new { MasterId = g.Key, Avg = g.Average(x => (double)x.Rating), Cnt = g.Count() })
            .ToListAsync();

        var cards = new List<MasterCard>(masters.Count);
        foreach (var m in masters)
        {
            var next = await _slots.GetNextAvailableSlotAsync(m.MasterId, BranchId, ServiceId, horizonDays: 14);
            var rating = ratings.FirstOrDefault(r => r.MasterId == m.MasterId);
            cards.Add(new MasterCard(m, next, rating?.Avg, rating?.Cnt ?? 0));
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
