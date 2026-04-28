using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using BarbershopCrm.Web.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

/// <summary>
/// Запись клиента в лист ожидания на тот случай, когда в нужном окне нет
/// свободных слотов. Поддерживает гостей (без регистрации) и авторизованных
/// клиентов; конкретный мастер опционален — null означает «любой в филиале».
/// </summary>
public class WaitlistModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public WaitlistModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [BindProperty] public WaitlistInput Input { get; set; } = new();
    public SelectList Branches { get; private set; } = default!;
    public SelectList Services { get; private set; } = default!;
    public SelectList Masters { get; private set; } = default!;
    public bool IsAuthenticated { get; private set; }
    [TempData] public bool Saved { get; set; }

    public class WaitlistInput
    {
        [Required(ErrorMessage = "Выберите филиал.")]
        public int BranchId { get; set; }
        [Required(ErrorMessage = "Выберите услугу.")]
        public int ServiceId { get; set; }
        public int? MasterId { get; set; }
        [Required(ErrorMessage = "Укажите дату начала окна.")]
        public DateOnly DateFrom { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        [Required(ErrorMessage = "Укажите дату конца окна.")]
        public DateOnly DateTo { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        [StringLength(160)] public string? GuestName { get; set; }
        [StringLength(20)] public string? GuestPhone { get; set; }
        [StringLength(500, ErrorMessage = "Комментарий не должен быть длиннее 500 символов.")]
        public string? Note { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? branchId, int? serviceId, int? masterId)
    {
        await LoadDictsAsync();
        IsAuthenticated = User.Identity?.IsAuthenticated == true;

        if (branchId is not null) Input.BranchId = branchId.Value;
        if (serviceId is not null) Input.ServiceId = serviceId.Value;
        if (masterId is > 0) Input.MasterId = masterId.Value;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadDictsAsync();
        IsAuthenticated = User.Identity?.IsAuthenticated == true;

        if (Input.DateTo < Input.DateFrom)
        {
            ModelState.AddModelError(nameof(Input.DateTo), "Дата окончания не может быть раньше начала.");
        }

        if (!IsAuthenticated)
        {
            if (string.IsNullOrWhiteSpace(Input.GuestName))
                ModelState.AddModelError(nameof(Input.GuestName), "Укажите ваше имя.");
            if (string.IsNullOrWhiteSpace(Input.GuestPhone) || !PhoneUtil.IsValid(Input.GuestPhone))
                ModelState.AddModelError(nameof(Input.GuestPhone), "Укажите корректный телефон.");
        }

        if (!ModelState.IsValid) return Page();

        int? personaId = null;
        if (IsAuthenticated)
        {
            var user = await _userManager.GetUserAsync(User);
            personaId = user?.PersonaId;
        }

        _db.WaitlistEntries.Add(new WaitlistEntry
        {
            PersonaId = personaId,
            GuestName = personaId is null ? Input.GuestName?.Trim() : null,
            GuestPhone = personaId is null && Input.GuestPhone is not null
                ? PhoneUtil.Normalize(Input.GuestPhone) : null,
            BranchId = Input.BranchId,
            ServiceId = Input.ServiceId,
            MasterId = Input.MasterId is > 0 ? Input.MasterId : null,
            DateFrom = Input.DateFrom,
            DateTo = Input.DateTo,
            Note = string.IsNullOrWhiteSpace(Input.Note) ? null : Input.Note.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        Saved = true;
        return RedirectToPage();
    }

    private async Task LoadDictsAsync()
    {
        Branches = new SelectList(await _db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync(), "BranchId", "Name");
        Services = new SelectList(await _db.Services.AsNoTracking().OrderBy(s => s.Name).ToListAsync(), "ServiceId", "Name");
        var masters = await _db.Masters.AsNoTracking().Include(m => m.Persona)
            .OrderBy(m => m.Persona.LastName).ToListAsync();
        Masters = new SelectList(masters.Select(m => new
        {
            m.MasterId,
            FullName = $"{m.Persona.LastName} {m.Persona.FirstName}".Trim()
        }), "MasterId", "FullName");
    }
}
