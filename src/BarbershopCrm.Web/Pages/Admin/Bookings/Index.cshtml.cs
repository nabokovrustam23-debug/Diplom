using BarbershopCrm.Domain.Entities;
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
    public IList<Branch> Branches { get; private set; } = new List<Branch>();
    public IList<Master> Masters { get; private set; } = new List<Master>();

    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? DateFilter { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public int? BranchId { get; set; }
    [BindProperty(SupportsGet = true)] public int? MasterId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public async Task OnGetAsync()
    {
        Branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
        Masters = await _db.Masters.AsNoTracking()
            .Include(m => m.Persona)
            .OrderBy(m => m.Persona.LastName)
            .ToListAsync();

        var q = _db.Bookings
            .Include(b => b.Branch)
            .Include(b => b.Service)
            .Include(b => b.Master).ThenInclude(m => m.Persona)
            .Include(b => b.Client).ThenInclude(c => c.Persona)
            .AsNoTracking()
            .AsQueryable();

        if (Enum.TryParse<BookingStatus>(StatusFilter, out var s))
            q = q.Where(b => b.Status == s);

        // Быстрые пресеты по дате имеют приоритет над пользовательским диапазоном.
        var today = DateTime.UtcNow.Date;
        switch (DateFilter)
        {
            case "today":
                q = q.Where(b => b.StartDateTime >= today && b.StartDateTime < today.AddDays(1));
                break;
            case "tomorrow":
                q = q.Where(b => b.StartDateTime >= today.AddDays(1) && b.StartDateTime < today.AddDays(2));
                break;
            case "week":
                q = q.Where(b => b.StartDateTime >= today && b.StartDateTime < today.AddDays(7));
                break;
            case "past":
                q = q.Where(b => b.StartDateTime < today);
                break;
            default:
                if (FromDate.HasValue)
                    q = q.Where(b => b.StartDateTime >= FromDate.Value.ToDateTime(TimeOnly.MinValue));
                if (ToDate.HasValue)
                    q = q.Where(b => b.StartDateTime < ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
                break;
        }

        if (BranchId.HasValue && BranchId.Value > 0)
            q = q.Where(b => b.BranchId == BranchId.Value);

        if (MasterId.HasValue && MasterId.Value > 0)
            q = q.Where(b => b.MasterId == MasterId.Value);

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var needle = Q.Trim();
            q = q.Where(b =>
                b.Client.Persona.LastName.Contains(needle) ||
                b.Client.Persona.FirstName.Contains(needle) ||
                b.Client.Persona.Phone.Contains(needle));
        }

        Bookings = await q
            .OrderByDescending(b => b.StartDateTime)
            .Take(200)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, BookingStatus status)
    {
        var b = await _db.Bookings.FindAsync(id);
        if (b is not null)
        {
            b.Status = status;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new
        {
            StatusFilter,
            DateFilter,
            FromDate = FromDate?.ToString("yyyy-MM-dd"),
            ToDate = ToDate?.ToString("yyyy-MM-dd"),
            BranchId,
            MasterId,
            Q
        });
    }
}
