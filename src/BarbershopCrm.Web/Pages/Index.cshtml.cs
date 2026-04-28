using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public IList<Branch> Branches { get; private set; } = new List<Branch>();
    public int MasterCount { get; private set; }
    public int ServiceCount { get; private set; }

    /// <summary>Города филиалов (первый сегмент адреса до запятой).</summary>
    public string CitiesLine { get; private set; } = string.Empty;

    public async Task OnGetAsync()
    {
        Branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
        MasterCount = await _db.Masters.AsNoTracking().CountAsync(m => m.IsActive);
        ServiceCount = await _db.Services.AsNoTracking().CountAsync(s => s.IsActive);
        CitiesLine = string.Join(" · ", Branches
            .Select(b => b.Address?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
