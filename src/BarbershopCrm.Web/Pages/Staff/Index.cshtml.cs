using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using BarbershopCrm.Web.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace BarbershopCrm.Web.Pages.Staff;

/// <summary>
/// Кабинет мастера: список предстоящих и прошлых записей, быстрое изменение
/// статуса (подтверждено / завершено / клиент не пришёл). Доступен самому
/// мастеру, администратору филиала и владельцу сети. Мастер видит только
/// свои записи (PersonaId у учётной записи совпадает с PersonaId мастера).
/// </summary>
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public Domain.Entities.Master? CurrentMaster { get; private set; }
    public IList<Domain.Entities.Booking> UpcomingBookings { get; private set; } = new List<Domain.Entities.Booking>();
    public IList<Domain.Entities.Booking> PastBookings { get; private set; } = new List<Domain.Entities.Booking>();

    /// <summary>
    /// Список мастеров для выбора (когда в кабинет входит владелец или
    /// администратор — они могут переключаться между мастерами). Пусто для
    /// самого мастера (у него только свой контекст).
    /// </summary>
    public IList<Domain.Entities.Master> MasterOptions { get; private set; } = new List<Domain.Entities.Master>();

    [BindProperty(SupportsGet = true)]
    public int? MasterId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadCurrentMasterAsync())
        {
            return Forbid();
        }
        await LoadBookingsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSetStatusAsync(int bookingId, BookingStatus status)
    {
        if (!await LoadCurrentMasterAsync())
        {
            return Forbid();
        }

        var booking = await _db.Bookings.FindAsync(bookingId);
        if (booking is null || booking.MasterId != CurrentMaster!.MasterId)
        {
            return Forbid();
        }

        // Мастер может закрыть запись: подтвердить, отметить выполненной или
        // поставить «не пришёл». Полную отмену оставляем администратору.
        if (status is BookingStatus.Confirmed or BookingStatus.Completed or BookingStatus.NoShow)
        {
            var oldStatus = booking.Status;
            booking.Status = status;
            AuditLogger.Log(_db, User, "MasterChangeStatus", "Booking", booking.BookingId.ToString(),
                $"from={oldStatus} to={status}");
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { MasterId });
    }

    private async Task<bool> LoadCurrentMasterAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return false;

        var isMaster = User.IsInRole(IdentitySeeder.MasterRole);
        var isStaff = User.IsInRole(IdentitySeeder.OwnerRole) || User.IsInRole(IdentitySeeder.AdminRole);

        if (isMaster)
        {
            // Учётка мастера — всегда привязана к его Persona → Master.
            CurrentMaster = await _db.Masters
                .Include(m => m.Persona)
                .FirstOrDefaultAsync(m => m.PersonaId == user.PersonaId);
            if (CurrentMaster is not null) MasterId = CurrentMaster.MasterId;
            return CurrentMaster is not null;
        }

        if (!isStaff) return false;

        // Владелец/администратор может просмотреть кабинет любого мастера.
        MasterOptions = await _db.Masters
            .AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.IsActive)
            .OrderBy(m => m.Persona.LastName)
            .ToListAsync();

        if (MasterOptions.Count == 0) return false;

        MasterId ??= MasterOptions[0].MasterId;
        CurrentMaster = MasterOptions.FirstOrDefault(m => m.MasterId == MasterId.Value);
        return CurrentMaster is not null;
    }

    private async Task LoadBookingsAsync()
    {
        if (CurrentMaster is null) return;

        var all = await _db.Bookings
            .AsNoTracking()
            .Include(b => b.Branch)
            .Include(b => b.Service)
            .Include(b => b.Client).ThenInclude(c => c.Persona)
            .Where(b => b.MasterId == CurrentMaster.MasterId)
            .OrderByDescending(b => b.StartDateTime)
            .ToListAsync();

        var now = DateTime.UtcNow;
        UpcomingBookings = all.Where(b => b.StartDateTime >= now).OrderBy(b => b.StartDateTime).ToList();
        PastBookings = all.Where(b => b.StartDateTime < now).Take(30).ToList();
    }
}
