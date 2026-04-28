using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Web.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Waitlist;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<WaitlistEntry> Items { get; private set; } = new List<WaitlistEntry>();
    [TempData] public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        Items = await _db.WaitlistEntries.AsNoTracking()
            .Include(w => w.Persona)
            .Include(w => w.Branch)
            .Include(w => w.Service)
            .Include(w => w.Master).ThenInclude(m => m!.Persona)
            .Where(w => !w.IsClosed)
            .OrderByDescending(w => w.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostNotifyAsync(int id)
    {
        var entry = await _db.WaitlistEntries.FirstOrDefaultAsync(w => w.WaitlistEntryId == id);
        if (entry is null) return NotFound();
        entry.NotifiedAtUtc = DateTime.UtcNow;
        AuditLogger.Log(_db, User, "WaitlistNotify", "WaitlistEntry", id.ToString());
        await _db.SaveChangesAsync();
        StatusMessage = "Заявка отмечена как «уведомлён».";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCloseAsync(int id)
    {
        var entry = await _db.WaitlistEntries.FirstOrDefaultAsync(w => w.WaitlistEntryId == id);
        if (entry is null) return NotFound();
        entry.IsClosed = true;
        AuditLogger.Log(_db, User, "WaitlistClose", "WaitlistEntry", id.ToString());
        await _db.SaveChangesAsync();
        StatusMessage = "Заявка закрыта.";
        return RedirectToPage();
    }
}
