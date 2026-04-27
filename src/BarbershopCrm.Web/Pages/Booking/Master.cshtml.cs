using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

public class MasterModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public MasterModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ServiceId { get; set; }

    public Branch? Branch { get; private set; }
    public Domain.Entities.Service? Service { get; private set; }
    public IReadOnlyList<Master> Masters { get; private set; } = Array.Empty<Master>();

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

        Masters = await _db.Masters
            .AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.IsActive
                && _db.MasterBranches.Any(mb => mb.MasterId == m.MasterId && mb.BranchId == BranchId)
                && _db.MasterServices.Any(ms => ms.MasterId == m.MasterId && ms.ServiceId == ServiceId))
            .OrderBy(m => m.Persona.LastName)
            .ToListAsync();

        return Page();
    }
}
