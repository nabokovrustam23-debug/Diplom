using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Web.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Branches;

public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DeleteModel(ApplicationDbContext db) => _db = db;

    public Branch? Branch { get; private set; }
    public int BookingsCount { get; private set; }
    public int MastersCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchId == id);
        if (Branch is null) return RedirectToPage("Index");
        BookingsCount = await _db.Bookings.CountAsync(b => b.BranchId == id);
        MastersCount = await _db.MasterBranches.CountAsync(mb => mb.BranchId == id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var branch = await _db.Branches.FindAsync(id);
        if (branch is not null)
        {
            AuditLogger.Log(_db, User, "Delete", "Branch", branch.BranchId.ToString(),
                $"name={branch.Name}");
            _db.Branches.Remove(branch);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("Index");
    }
}
