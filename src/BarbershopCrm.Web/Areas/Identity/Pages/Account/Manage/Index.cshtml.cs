using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
    }

    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = "Т";
    public string RoleLabel { get; set; } = string.Empty;
    public int BookingsCount { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Укажите фамилию")]
        [StringLength(100)]
        [Display(Name = "Фамилия")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите имя")]
        [StringLength(100)]
        [Display(Name = "Имя")]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Отчество")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Укажите телефон")]
        [Phone(ErrorMessage = "Некорректный номер телефона")]
        [StringLength(20)]
        [Display(Name = "Телефон")]
        public string Phone { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound("Не удалось получить данные пользователя.");
        }

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound("Не удалось получить данные пользователя.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.PersonaId == user.PersonaId);
        if (persona is null)
        {
            ModelState.AddModelError(string.Empty, "Профиль не найден в базе данных.");
            await LoadAsync(user);
            return Page();
        }

        persona.LastName = Input.LastName.Trim();
        persona.FirstName = Input.FirstName.Trim();
        persona.MiddleName = string.IsNullOrWhiteSpace(Input.MiddleName) ? null : Input.MiddleName.Trim();
        persona.Phone = Input.Phone.Trim();

        // Синхронизируем телефон в Identity-учётке.
        if (user.PhoneNumber != persona.Phone)
        {
            await _userManager.SetPhoneNumberAsync(user, persona.Phone);
        }

        await _db.SaveChangesAsync();
        await _signInManager.RefreshSignInAsync(user);

        StatusMessage = "Профиль обновлён.";
        return RedirectToPage();
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        var persona = await _db.Personas.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonaId == user.PersonaId);

        Email = user.Email ?? string.Empty;

        if (persona is not null)
        {
            Input.LastName = persona.LastName;
            Input.FirstName = persona.FirstName;
            Input.MiddleName = persona.MiddleName;
            Input.Phone = persona.Phone;

            FullName = string.Join(' ', new[] { persona.LastName, persona.FirstName, persona.MiddleName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(FullName))
            {
                FullName = Email;
            }

            Initials = string.Concat(
                (persona.LastName.Length > 0 ? persona.LastName[..1] : string.Empty),
                (persona.FirstName.Length > 0 ? persona.FirstName[..1] : string.Empty)
            ).ToUpper();
            if (string.IsNullOrWhiteSpace(Initials))
            {
                Initials = (Email.Length > 0 ? Email[..1] : "Т").ToUpper();
            }
        }
        else
        {
            FullName = Email;
            Input.Phone = user.PhoneNumber ?? string.Empty;
            Initials = (Email.Length > 0 ? Email[..1] : "Т").ToUpper();
        }

        var roles = await _userManager.GetRolesAsync(user);
        RoleLabel = roles.FirstOrDefault() switch
        {
            "Owner" => "Владелец",
            "Admin" => "Администратор",
            "Master" => "Мастер",
            "Client" => "Клиент",
            _ => string.Empty
        };

        // Кол-во записей клиента (если он клиент).
        if (persona is not null)
        {
            BookingsCount = await _db.Bookings
                .Where(b => b.Client.PersonaId == persona.PersonaId)
                .CountAsync();
        }
    }
}
