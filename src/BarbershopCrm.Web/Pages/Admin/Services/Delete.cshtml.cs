using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Services;

public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DeleteModel(ApplicationDbContext db) => _db = db;

    public Service? Service { get; private set; }
    public int BookingsCount { get; private set; }
    public int MastersCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.ServiceId == id);
        if (Service is null) return RedirectToPage("Index");
        BookingsCount = await _db.Bookings.CountAsync(b => b.ServiceId == id);
        MastersCount = await _db.MasterServices.CountAsync(ms => ms.ServiceId == id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var s = await _db.Services.FindAsync(id);
        if (s is not null)
        {
            _db.Services.Remove(s);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("Index");
    }
}
