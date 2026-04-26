using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Branches;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<Branch> Branches { get; private set; } = new List<Branch>();

    public async Task OnGetAsync()
    {
        Branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
    }
}
