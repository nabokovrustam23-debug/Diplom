using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Staff;

/// <summary>
/// Страница самостоятельной правки профиля мастера: должность и био.
/// Активность/привязку к филиалам и услугам правит только владелец —
/// здесь мастер меняет только то, что касается его самоописания.
/// </summary>
[Authorize(Roles = IdentitySeeder.MasterRole)]
public class ProfileModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string FullName { get; private set; } = string.Empty;
    public string BranchName { get; private set; } = "—";

    /// <summary>Сводная статистика мастера за последние 30 дней — даёт ощущение
    /// «кабинет не пустой» и помогает мастеру видеть собственную нагрузку.</summary>
    public int CompletedLast30 { get; private set; }
    public int CancelledLast30 { get; private set; }
    public int NoShowLast30 { get; private set; }
    public int UpcomingCount { get; private set; }
    public decimal RevenueLast30 { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Укажите должность")]
        [StringLength(100)]
        [Display(Name = "Должность")]
        public string Position { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "О себе")]
        public string? Bio { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var master = await LoadAsync();
        if (master is null) return Forbid();
        Input.Position = master.Position;
        Input.Bio = master.Bio;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var master = await LoadAsync();
        if (master is null) return Forbid();
        if (!ModelState.IsValid) return Page();

        master.Position = Input.Position.Trim();
        master.Bio = string.IsNullOrWhiteSpace(Input.Bio) ? null : Input.Bio.Trim();
        await _db.SaveChangesAsync();
        StatusMessage = "Профиль сохранён.";
        return RedirectToPage();
    }

    private async Task<Domain.Entities.Master?> LoadAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return null;
        var master = await _db.Masters
            .Include(m => m.Persona)
            .Include(m => m.MasterBranches).ThenInclude(mb => mb.Branch)
            .FirstOrDefaultAsync(m => m.PersonaId == user.PersonaId);
        if (master is null) return null;
        FullName = $"{master.Persona.LastName} {master.Persona.FirstName}";
        BranchName = master.MasterBranches.FirstOrDefault()?.Branch.Name ?? "—";

        var since = DateTime.UtcNow.Date.AddDays(-30);
        var bookings = await _db.Bookings
            .AsNoTracking()
            .Where(b => b.MasterId == master.MasterId && b.StartDateTime >= since)
            .Join(_db.Services, b => b.ServiceId, s => s.ServiceId, (b, s) => new { b.Status, s.Price })
            .ToListAsync();
        CompletedLast30 = bookings.Count(b => b.Status == BookingStatus.Completed);
        CancelledLast30 = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        NoShowLast30 = bookings.Count(b => b.Status == BookingStatus.NoShow);
        RevenueLast30 = bookings.Where(b => b.Status == BookingStatus.Completed).Sum(b => b.Price);

        var now = DateTime.UtcNow;
        UpcomingCount = await _db.Bookings.CountAsync(b =>
            b.MasterId == master.MasterId
            && b.StartDateTime >= now
            && (b.Status == BookingStatus.Created || b.Status == BookingStatus.Confirmed));
        return master;
    }
}
