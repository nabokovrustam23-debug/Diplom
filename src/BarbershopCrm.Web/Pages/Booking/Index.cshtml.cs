using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public IReadOnlyList<Branch> Branches { get; private set; } = Array.Empty<Branch>();

    public async Task OnGetAsync()
    {
        Branches = await _db.Branches
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .ToListAsync();
    }
}
