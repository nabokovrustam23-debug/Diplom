using BarbershopCrm.Domain.Enums;
using BookingEntity = BarbershopCrm.Domain.Entities.Booking;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Bookings;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<BookingEntity> Bookings { get; private set; } = new List<BookingEntity>();

    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }

    public async Task OnGetAsync()
    {
        var q = _db.Bookings
            .Include(b => b.Branch)
            .Include(b => b.Service)
            .Include(b => b.Master).ThenInclude(m => m.Persona)
            .Include(b => b.Client).ThenInclude(c => c.Persona)
            .AsNoTracking()
            .OrderByDescending(b => b.StartDateTime)
            .AsQueryable();

        if (Enum.TryParse<BookingStatus>(StatusFilter, out var s))
            q = q.Where(b => b.Status == s);

        Bookings = await q.Take(200).ToListAsync();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, BookingStatus status)
    {
        var b = await _db.Bookings.FindAsync(id);
        if (b is not null)
        {
            b.Status = status;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}
