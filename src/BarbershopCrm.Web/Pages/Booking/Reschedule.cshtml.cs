using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using BarbershopCrm.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

/// <summary>
/// Перенос существующей записи на новый слот. Клиент может перенести свою
/// будущую активную запись; тот же мастер, услуга и филиал — меняется только
/// дата/время. Для занятия нового слота используется тот же слот-алгоритм:
/// старый слот освобождается путём обновления StartDateTime (без удаления
/// Booking — история переноса остаётся в поле UpdatedAt при необходимости).
/// </summary>
[Authorize]
public class RescheduleModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISlotService _slots;
    private readonly UserManager<ApplicationUser> _userManager;

    public RescheduleModel(ApplicationDbContext db, ISlotService slots, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _slots = slots;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)]
    public int BookingId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Date { get; set; }

    public Domain.Entities.Booking? Current { get; private set; }
    public IReadOnlyList<DateOnly> AvailableDates { get; private set; } = Array.Empty<DateOnly>();
    public IReadOnlyList<TimeOnly> AvailableSlots { get; private set; } = Array.Empty<TimeOnly>();

    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return RedirectToPage("/Account/Manage/Index", new { area = "Identity" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        AvailableDates = Enumerable.Range(0, 14).Select(i => today.AddDays(i)).ToList();
        Date ??= AvailableDates[0];

        var slots = await _slots.GetAvailableSlotsAsync(
            Current!.MasterId, Current.BranchId, Current.ServiceId, Date.Value);

        // Если пытаются перенести на тот же день — отфильтруем «текущий» слот,
        // чтобы в списке не было времени, которое уже занято самой этой записью
        // (алгоритм учитывает её как занятую, и её же не предложит; здесь
        // мы явно дублируем это намерение для наглядности).
        var selfTime = TimeOnly.FromDateTime(Current.StartDateTime);
        if (Date.Value == DateOnly.FromDateTime(Current.StartDateTime))
        {
            slots = slots.Where(t => t != selfTime).ToList();
        }
        AvailableSlots = slots;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(DateOnly newDate, TimeOnly newTime)
    {
        if (!await LoadAsync()) return RedirectToPage("/Account/Manage/Index", new { area = "Identity" });

        // Проверяем доступность выбранного слота.
        var available = await _slots.GetAvailableSlotsAsync(
            Current!.MasterId, Current.BranchId, Current.ServiceId, newDate);

        if (!available.Contains(newTime))
        {
            StatusMessage = "Этот слот уже занят. Выберите другой.";
            return RedirectToPage(new { BookingId = Current.BookingId, Date = newDate });
        }

        // Сохраняем исходное время переноса для аудита и отображения клиенту.
        if (Current.RescheduledFromUtc is null)
        {
            Current.RescheduledFromUtc = Current.StartDateTime;
        }
        Current.StartDateTime = newDate.ToDateTime(newTime);
        // Сбрасываем статус на Created: мастеру нужно подтвердить перенос.
        if (Current.Status == BookingStatus.Confirmed) Current.Status = BookingStatus.Created;

        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = $"Запись перенесена на {newDate:dd.MM} в {newTime:HH\\:mm}.";
        // Сотрудников отправляем в их рабочие экраны, клиента — в его кабинет.
        if (User.IsInRole(IdentitySeeder.OwnerRole) || User.IsInRole(IdentitySeeder.AdminRole))
        {
            return RedirectToPage("/Admin/Bookings/Index");
        }
        if (User.IsInRole(IdentitySeeder.MasterRole))
        {
            return RedirectToPage("/Staff/Index");
        }
        return RedirectToPage("/Account/Manage/Index", new { area = "Identity" });
    }

    private async Task<bool> LoadAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return false;

        Current = await _db.Bookings
            .Include(b => b.Branch)
            .Include(b => b.Service)
            .Include(b => b.Master).ThenInclude(m => m.Persona)
            .Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.BookingId == BookingId);

        if (Current is null) return false;

        // Проверяем права: перенести может сам клиент или сотрудник (Owner/Admin/Master).
        var isClient = Current.Client.PersonaId == user.PersonaId;
        var isStaff = User.IsInRole(IdentitySeeder.OwnerRole)
                   || User.IsInRole(IdentitySeeder.AdminRole)
                   || User.IsInRole(IdentitySeeder.MasterRole);
        if (!isClient && !isStaff) return false;

        // Прошлое/отменённое/завершённое переносу не подлежит.
        if (Current.StartDateTime < DateTime.UtcNow) return false;
        if (Current.Status is BookingStatus.Cancelled
            or BookingStatus.Completed
            or BookingStatus.NoShow) return false;

        return true;
    }
}
