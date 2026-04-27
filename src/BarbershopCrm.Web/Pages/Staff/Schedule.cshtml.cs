using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace BarbershopCrm.Web.Pages.Staff;

/// <summary>
/// Календарная сетка расписания мастера на неделю. Владелец/администратор
/// видят расписания всех мастеров и могут добавлять исключения (отпуск,
/// больничный, занятость под обучение). Мастер видит свой календарь в режиме
/// чтения и может добавить разовое исключение на день.
/// </summary>
public class ScheduleModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ScheduleModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)]
    public int? MasterId { get; set; }

    /// <summary>Понедельник выбранной недели. По умолчанию — текущая неделя.</summary>
    [BindProperty(SupportsGet = true)]
    public DateOnly? WeekStart { get; set; }

    public Domain.Entities.Master? CurrentMaster { get; private set; }
    public IList<Domain.Entities.Master> MasterOptions { get; private set; } = new List<Domain.Entities.Master>();
    public IReadOnlyList<DateOnly> WeekDays { get; private set; } = Array.Empty<DateOnly>();
    public IList<WorkSchedule> WeekSchedules { get; private set; } = new List<WorkSchedule>();
    public IList<Domain.Entities.Booking> WeekBookings { get; private set; } = new List<Domain.Entities.Booking>();

    /// <summary>Тайм-лайн по часам 08:00–22:00 шагом 1 час для отрисовки сетки.</summary>
    public IReadOnlyList<TimeOnly> HourRows { get; private set; }
        = Enumerable.Range(8, 15).Select(h => new TimeOnly(h, 0)).ToList();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadContextAsync()) return Forbid();
        await LoadWeekAsync();
        return Page();
    }

    /// <summary>
    /// Добавляет разовое исключение (отпуск/больничный/занятость) на день.
    /// Время по умолчанию закрывает весь рабочий день 00:00–23:59.
    /// </summary>
    public async Task<IActionResult> OnPostAddExceptionAsync(
        int masterId,
        DateOnly workDate,
        ScheduleType scheduleType,
        TimeOnly? startTime,
        TimeOnly? endTime)
    {
        if (!await LoadContextAsync()) return Forbid();

        // Мастер может добавить исключение только себе.
        if (User.IsInRole(IdentitySeeder.MasterRole) && CurrentMaster?.MasterId != masterId)
        {
            return Forbid();
        }

        var start = startTime ?? new TimeOnly(0, 0);
        var end = endTime ?? new TimeOnly(23, 59);
        if (end <= start)
        {
            ModelState.AddModelError(string.Empty, "Время окончания должно быть позже времени начала.");
            await LoadWeekAsync();
            return Page();
        }

        // Берём филиал из первого существующего расписания мастера; если нет —
        // из MasterBranches (первый попавшийся, у нас мастер привязан к 1 филиалу).
        var branchId = await _db.WorkSchedules
            .Where(w => w.MasterId == masterId)
            .Select(w => (int?)w.BranchId)
            .FirstOrDefaultAsync()
            ?? await _db.MasterBranches
                .Where(mb => mb.MasterId == masterId)
                .Select(mb => (int?)mb.BranchId)
                .FirstOrDefaultAsync();

        if (branchId is null)
        {
            ModelState.AddModelError(string.Empty, "Мастер не привязан ни к одному филиалу.");
            await LoadWeekAsync();
            return Page();
        }

        _db.WorkSchedules.Add(new WorkSchedule
        {
            MasterId = masterId,
            BranchId = branchId.Value,
            WorkDate = workDate,
            StartTime = start,
            EndTime = end,
            ScheduleType = scheduleType
        });
        await _db.SaveChangesAsync();

        return RedirectToPage(new { MasterId = masterId, WeekStart = WeekStart?.ToString("yyyy-MM-dd") });
    }

    /// <summary>
    /// Удаляет запись расписания (обычно — снятие исключения/блокировки).
    /// Доступно администратору, владельцу и самому мастеру.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!await LoadContextAsync()) return Forbid();

        var row = await _db.WorkSchedules.FindAsync(id);
        if (row is null) return RedirectToPage(new { MasterId, WeekStart = WeekStart?.ToString("yyyy-MM-dd") });

        if (User.IsInRole(IdentitySeeder.MasterRole) && row.MasterId != CurrentMaster?.MasterId)
        {
            return Forbid();
        }

        _db.WorkSchedules.Remove(row);
        await _db.SaveChangesAsync();
        return RedirectToPage(new { MasterId = row.MasterId, WeekStart = WeekStart?.ToString("yyyy-MM-dd") });
    }

    private async Task<bool> LoadContextAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return false;

        var isMaster = User.IsInRole(IdentitySeeder.MasterRole);
        var isStaff = User.IsInRole(IdentitySeeder.OwnerRole) || User.IsInRole(IdentitySeeder.AdminRole);

        if (isMaster)
        {
            CurrentMaster = await _db.Masters
                .Include(m => m.Persona)
                .FirstOrDefaultAsync(m => m.PersonaId == user.PersonaId);
            MasterId = CurrentMaster?.MasterId;
            return CurrentMaster is not null;
        }

        if (!isStaff) return false;

        MasterOptions = await _db.Masters
            .AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.IsActive)
            .OrderBy(m => m.Persona.LastName)
            .ToListAsync();

        MasterId ??= MasterOptions.FirstOrDefault()?.MasterId;
        CurrentMaster = await _db.Masters
            .Include(m => m.Persona)
            .FirstOrDefaultAsync(m => m.MasterId == MasterId);
        return CurrentMaster is not null;
    }

    private async Task LoadWeekAsync()
    {
        if (CurrentMaster is null) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var defaultMonday = today.AddDays(-((int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1));
        var monday = WeekStart ?? defaultMonday;
        WeekStart = monday;
        WeekDays = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList();

        var endExclusive = monday.AddDays(7);
        WeekSchedules = await _db.WorkSchedules
            .AsNoTracking()
            .Where(w => w.MasterId == CurrentMaster.MasterId
                     && w.WorkDate >= monday
                     && w.WorkDate < endExclusive)
            .OrderBy(w => w.WorkDate).ThenBy(w => w.StartTime)
            .ToListAsync();

        var from = monday.ToDateTime(TimeOnly.MinValue);
        var to = endExclusive.ToDateTime(TimeOnly.MinValue);
        WeekBookings = await _db.Bookings
            .AsNoTracking()
            .Include(b => b.Service)
            .Include(b => b.Client).ThenInclude(c => c.Persona)
            .Where(b => b.MasterId == CurrentMaster.MasterId
                     && b.Status != BookingStatus.Cancelled
                     && b.StartDateTime >= from
                     && b.StartDateTime < to)
            .ToListAsync();
    }

    public static string ScheduleTypeLabel(ScheduleType t) => t switch
    {
        ScheduleType.Work => "Смена",
        ScheduleType.Lunch => "Обед",
        ScheduleType.DayOff => "Выходной",
        ScheduleType.Vacation => "Отпуск",
        ScheduleType.SickLeave => "Больничный",
        _ => t.ToString()
    };
}
