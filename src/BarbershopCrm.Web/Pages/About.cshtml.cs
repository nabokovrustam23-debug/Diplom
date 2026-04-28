using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages;

/// <summary>Публичная страница «О сети»: цифры и описание подхода.</summary>
public class AboutModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public AboutModel(ApplicationDbContext db) => _db = db;

    public int BranchCount { get; private set; }
    public int MasterCount { get; private set; }
    public int ServiceCount { get; private set; }
    public int ClientCount { get; private set; }
    public int CompletedBookings { get; private set; }

    public string BranchWordForm => BranchCount switch
    {
        1 => "филиал",
        >= 2 and <= 4 => "филиала",
        _ => "филиалов",
    };

    public async Task OnGetAsync()
    {
        BranchCount = await _db.Branches.CountAsync();
        MasterCount = await _db.Masters.CountAsync(m => m.IsActive);
        ServiceCount = await _db.Services.CountAsync(s => s.IsActive);
        ClientCount = await _db.Clients.CountAsync();
        CompletedBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Completed);
    }
}
