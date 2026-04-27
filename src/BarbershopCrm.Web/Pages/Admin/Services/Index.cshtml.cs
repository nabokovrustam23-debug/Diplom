using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Services;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<Service> Services { get; private set; } = new List<Service>();

    public async Task OnGetAsync()
    {
        Services = await _db.Services.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
    }
}
