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
    public int BookingsTomorrow { get; private set; }
    public decimal RevenueThisWeek { get; private set; }
    public int CompletedThisWeek { get; private set; }
    public int CancelledThisWeek { get; private set; }
    public int NoShowThisWeek { get; private set; }
    public decimal AvgCheckThisWeek { get; private set; }

    public async Task OnGetAsync()
    {
        BranchCount = await _db.Branches.CountAsync();
        ServiceCount = await _db.Services.CountAsync();
        MasterCount = await _db.Masters.CountAsync(m => m.IsActive);
        ClientCount = await _db.Clients.CountAsync();
        BookingCount = await _db.Bookings.CountAsync();

        var today = DateTime.Today;
        // Неделя начинается с понедельника. Если сегодня воскресенье, DayOfWeek == 0.
        var dow = (int)today.DayOfWeek;
        var weekStart = today.AddDays(dow == 0 ? -6 : -(dow - 1));
        BookingsToday = await _db.Bookings.CountAsync(b => b.StartDateTime >= today && b.StartDateTime < today.AddDays(1));
        BookingsTomorrow = await _db.Bookings.CountAsync(b => b.StartDateTime >= today.AddDays(1) && b.StartDateTime < today.AddDays(2));
        BookingsThisWeek = await _db.Bookings.CountAsync(b => b.StartDateTime >= weekStart && b.Status != BookingStatus.Cancelled);

        var weekBookings = await _db.Bookings
            .Where(b => b.StartDateTime >= weekStart)
            .Join(_db.Services, b => b.ServiceId, s => s.ServiceId, (b, s) => new { b.Status, s.Price })
            .ToListAsync();
        var completed = weekBookings.Where(x => x.Status == BookingStatus.Completed).ToList();
        RevenueThisWeek = completed.Sum(x => x.Price);
        CompletedThisWeek = completed.Count;
        CancelledThisWeek = weekBookings.Count(x => x.Status == BookingStatus.Cancelled);
        NoShowThisWeek = weekBookings.Count(x => x.Status == BookingStatus.NoShow);
        AvgCheckThisWeek = completed.Count > 0 ? Math.Round(RevenueThisWeek / completed.Count, 0) : 0m;
    }
}
