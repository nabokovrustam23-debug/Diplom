using System.Text.Json;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using BarbershopCrm.Web.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Реализация требований 152-ФЗ для пользователя: экспорт всех персональных
/// данных в JSON и полное удаление аккаунта. Удаление обезличивает историю
/// посещений (записи остаются для финансовой отчётности, но без привязки
/// к конкретному человеку).
/// </summary>
public class PersonalDataModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PersonalDataModel> _log;

    public PersonalDataModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db,
        ILogger<PersonalDataModel> log)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _log = log;
    }

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostDownloadPersonalDataAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var persona = await _db.Personas.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonaId == user.PersonaId);
        var bookings = await _db.Bookings.AsNoTracking()
            .Where(b => b.Client.PersonaId == user.PersonaId)
            .Select(b => new
            {
                b.BookingId,
                b.StartDateTime,
                b.DurationMinutes,
                Status = b.Status.ToString(),
                Service = b.Service.Name,
                Branch = b.Branch.Name,
                Master = b.Master.Persona.LastName + " " + b.Master.Persona.FirstName,
                b.Notes,
                b.Wishes
            })
            .ToListAsync();
        var consents = await _db.ConsentLogs.AsNoTracking()
            .Where(c => c.PersonaId == user.PersonaId)
            .Select(c => new { c.PolicyVersion, c.GivenAtUtc, c.IpAddress })
            .ToListAsync();

        var payload = new
        {
            ExportedAtUtc = DateTime.UtcNow,
            Account = new
            {
                user.Email,
                user.UserName,
                user.PhoneNumber
            },
            Persona = persona is null ? null : new
            {
                persona.LastName,
                persona.FirstName,
                persona.MiddleName,
                persona.Phone,
                persona.Email,
                persona.BirthDate,
                Gender = persona.Gender.ToString()
            },
            Bookings = bookings,
            Consents = consents
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        AuditLogger.Log(_db, User, "ExportPersonalData", "Persona", user.PersonaId.ToString(), null);
        await _db.SaveChangesAsync();

        var fileName = $"personal-data-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        return File(bytes, "application/json", fileName);
    }

    public async Task<IActionResult> OnPostDeleteAccountAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var personaId = user.PersonaId;

        // Запрет удаления, если есть активные будущие записи: чтобы клиент
        // сначала их отменил (иначе мастер придёт в пустой слот).
        var hasFuture = await _db.Bookings.AnyAsync(b =>
            b.Client.PersonaId == personaId
            && b.StartDateTime > DateTime.UtcNow
            && b.Status != Domain.Enums.BookingStatus.Cancelled
            && b.Status != Domain.Enums.BookingStatus.Completed
            && b.Status != Domain.Enums.BookingStatus.NoShow);
        if (hasFuture)
        {
            StatusMessage = "Нельзя удалить аккаунт: есть активные записи. Сначала отмените их в кабинете.";
            return RedirectToPage();
        }

        // Обезличивание Persona: вместо физического удаления (FK-каскад
        // снёс бы историю заказов) подменяем ПДн на нейтральные значения.
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.PersonaId == personaId);
        if (persona is not null)
        {
            persona.LastName = "Удалённый";
            persona.FirstName = "клиент";
            persona.MiddleName = null;
            persona.Phone = $"+7-deleted-{personaId:0000000000}";
            persona.Email = null;
            persona.BirthDate = null;
        }

        AuditLogger.Log(_db, User, "DeleteAccount", "Persona", personaId.ToString(), null);
        await _db.SaveChangesAsync();

        // Удаляем саму учётку Identity (логин/пароль).
        await _userManager.DeleteAsync(user);
        await _signInManager.SignOutAsync();

        _log.LogInformation("Аккаунт PersonaId={PersonaId} удалён по запросу пользователя", personaId);
        return RedirectToPage("/Index", new { area = "" });
    }
}
