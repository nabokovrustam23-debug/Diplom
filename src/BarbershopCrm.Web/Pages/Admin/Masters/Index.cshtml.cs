using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Masters;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<Master> Masters { get; private set; } = new List<Master>();
    public Dictionary<int, int> BranchCounts { get; private set; } = new();
    public Dictionary<int, int> ServiceCounts { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Masters = await _db.Masters
            .Include(m => m.Persona)
            .AsNoTracking()
            .OrderBy(m => m.Persona.LastName)
            .ToListAsync();

        var ids = Masters.Select(m => m.MasterId).ToList();

        BranchCounts = await _db.MasterBranches
            .Where(mb => ids.Contains(mb.MasterId))
            .GroupBy(mb => mb.MasterId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        ServiceCounts = await _db.MasterServices
            .Where(ms => ids.Contains(ms.MasterId))
            .GroupBy(ms => ms.MasterId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }
}
