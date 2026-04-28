using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

public class SuccessModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public SuccessModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public int BookingId { get; set; }

    public Domain.Entities.Booking? Booking { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Booking = await _db.Bookings
            .AsNoTracking()
            .Include(b => b.Branch)
            .Include(b => b.Service)
            .Include(b => b.Master).ThenInclude(m => m.Persona)
            .Include(b => b.Client).ThenInclude(c => c.Persona)
            .FirstOrDefaultAsync(b => b.BookingId == BookingId);

        if (Booking is null)
        {
            return RedirectToPage("Index");
        }

        return Page();
    }
}
