using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public int BranchCount { get; private set; }
    public int ServiceCount { get; private set; }
    public int MasterCount { get; private set; }
    public int ClientCount { get; private set; }
    public int BookingCount { get; private set; }
    public int BookingsThisWeek { get; private set; }
    public int BookingsToday { get; private set; }
    public decimal RevenueThisWeek { get; private set; }

    public async Task OnGetAsync()
    {
        BranchCount = await _db.Branches.CountAsync();
        ServiceCount = await _db.Services.CountAsync();
        MasterCount = await _db.Masters.CountAsync(m => m.IsActive);
        ClientCount = await _db.Clients.CountAsync();
        BookingCount = await _db.Bookings.CountAsync();

        var today = DateTime.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        BookingsToday = await _db.Bookings.CountAsync(b => b.StartDateTime >= today && b.StartDateTime < today.AddDays(1));
        BookingsThisWeek = await _db.Bookings.CountAsync(b => b.StartDateTime >= weekStart && b.Status != BookingStatus.Cancelled);

        var weekPrices = await _db.Bookings
            .Where(b => b.StartDateTime >= weekStart && b.Status == BookingStatus.Completed)
            .Join(_db.Services, b => b.ServiceId, s => s.ServiceId, (b, s) => s.Price)
            .ToListAsync();
        RevenueThisWeek = weekPrices.Sum();
    }
}
