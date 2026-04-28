using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Web.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Masters;

public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DeleteModel(ApplicationDbContext db) => _db = db;

    public Master? Master { get; private set; }
    public int BookingsCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Master = await _db.Masters.Include(m => m.Persona).AsNoTracking()
            .FirstOrDefaultAsync(m => m.MasterId == id);
        if (Master is null) return RedirectToPage("Index");
        BookingsCount = await _db.Bookings.CountAsync(b => b.MasterId == id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, bool deactivateOnly = false)
    {
        var m = await _db.Masters.FindAsync(id);
        if (m is null) return RedirectToPage("Index");

        if (deactivateOnly)
        {
            m.IsActive = false;
            AuditLogger.Log(_db, User, "Deactivate", "Master", m.MasterId.ToString(), null);
        }
        else
        {
            AuditLogger.Log(_db, User, "Delete", "Master", m.MasterId.ToString(), null);
            _db.Masters.Remove(m);
        }
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
