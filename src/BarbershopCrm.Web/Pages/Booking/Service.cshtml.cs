using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

public class ServiceModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public ServiceModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    public Branch? Branch { get; private set; }

    public IReadOnlyList<Domain.Entities.Service> Services { get; private set; }
        = Array.Empty<Domain.Entities.Service>();

    public async Task<IActionResult> OnGetAsync()
    {
        Branch = await _db.Branches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BranchId == BranchId);

        if (Branch is null)
        {
            return RedirectToPage("Index");
        }

        // Услуги, которые умеет делать хотя бы один мастер этого филиала.
        Services = await (
            from s in _db.Services
            where _db.MasterServices.Any(ms =>
                ms.ServiceId == s.ServiceId
                && _db.MasterBranches.Any(mb =>
                    mb.MasterId == ms.MasterId && mb.BranchId == BranchId))
            orderby s.DisplayOrder, s.Name
            select s
        ).AsNoTracking().ToListAsync();

        return Page();
    }
}
