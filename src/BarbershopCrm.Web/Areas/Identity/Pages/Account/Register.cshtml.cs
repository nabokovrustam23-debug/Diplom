using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using BarbershopCrm.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db,
        ILogger<RegisterModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

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

        [Required(ErrorMessage = "Укажите телефон")]
        [StringLength(20)]
        [Phone(ErrorMessage = "Некорректный номер телефона")]
        [Display(Name = "Телефон")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите e-mail")]
        [EmailAddress(ErrorMessage = "Некорректный формат e-mail")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Придумайте пароль")]
        [StringLength(100, ErrorMessage = "Пароль должен быть от {2} до {1} символов.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Повторите пароль")]
        [Compare("Password", ErrorMessage = "Пароли не совпадают.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Согласие на обработку персональных данных")]
        public bool ConsentGiven { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!Input.ConsentGiven)
        {
            ModelState.AddModelError(nameof(Input.ConsentGiven),
                "Чтобы создать аккаунт, поставьте галочку согласия на обработку персональных данных.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Нормализуем номер к каноническому виду «+7XXXXXXXXXX» — иначе
        // одни и те же абоненты заводят дубль Persona при разных вариантах ввода.
        if (!PhoneUtil.IsValid(Input.Phone))
        {
            ModelState.AddModelError(nameof(Input.Phone), "Некорректный номер телефона.");
            return Page();
        }
        var phone = PhoneUtil.Normalize(Input.Phone);
        var email = Input.Email.Trim();

        // Если по этому телефону уже есть учётная запись — предлагаем войти,
        // а не плодить дубль. Иначе при двух регистрациях с одинаковым телефоном
        // мы получали бы две учётки на одну Persona, что путает аналитику.
        var personaByPhone = await _db.Personas.FirstOrDefaultAsync(p => p.Phone == phone);
        if (personaByPhone is not null)
        {
            var existingUser = await _userManager.Users.FirstOrDefaultAsync(u => u.PersonaId == personaByPhone.PersonaId);
            if (existingUser is not null)
            {
                ModelState.AddModelError(nameof(Input.Phone),
                    $"По этому телефону уже есть учётная запись ({existingUser.Email}). Войдите или восстановите пароль.");
                return Page();
            }
        }

        // Persona — общая сущность; ищем существующую запись по телефону, иначе
        // создаём новую. Это соответствует домен-модели «Persona-паттерна».
        var persona = personaByPhone;
        if (persona is null)
        {
            persona = new Persona
            {
                LastName = Input.LastName.Trim(),
                FirstName = Input.FirstName.Trim(),
                Phone = phone,
                Email = email
            };
            _db.Personas.Add(persona);
            await _db.SaveChangesAsync();
        }
        else
        {
            // Обновляем имя/e-mail на свежие — в случае гостевой записи
            // Persona мог быть создан без e-mail.
            persona.LastName = Input.LastName.Trim();
            persona.FirstName = Input.FirstName.Trim();
            if (string.IsNullOrWhiteSpace(persona.Email))
            {
                persona.Email = email;
            }
            await _db.SaveChangesAsync();
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PersonaId = persona.PersonaId,
            PhoneNumber = phone
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            _logger.LogInformation("Зарегистрирован новый пользователь {Email}.", email);
            await _userManager.AddToRoleAsync(user, IdentitySeeder.ClientRole);

            // Привязываем Client к Persona, если ещё не привязан.
            var clientExists = await _db.Clients.AnyAsync(c => c.PersonaId == persona.PersonaId);
            if (!clientExists)
            {
                _db.Clients.Add(new Client { PersonaId = persona.PersonaId });
                await _db.SaveChangesAsync();
            }

            // Журнал согласий на обработку ПДн (152-ФЗ).
            _db.ConsentLogs.Add(new ConsentLog
            {
                PersonaId = persona.PersonaId,
                GuestPhone = phone,
                PolicyVersion = "1.0",
                GivenAtUtc = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString().Length > 512
                    ? Request.Headers.UserAgent.ToString()[..512]
                    : Request.Headers.UserAgent.ToString()
            });
            await _db.SaveChangesAsync();

            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, TranslateError(error));
        }

        return Page();
    }

    /// <summary>Перевод стандартных сообщений Identity на русский.</summary>
    private static string TranslateError(IdentityError error) => error.Code switch
    {
        "DuplicateUserName" => "Пользователь с таким e-mail уже зарегистрирован.",
        "DuplicateEmail" => "Этот e-mail уже используется.",
        "PasswordTooShort" => "Пароль слишком короткий.",
        "PasswordRequiresDigit" => "Пароль должен содержать хотя бы одну цифру.",
        "PasswordRequiresLower" => "Пароль должен содержать хотя бы одну строчную букву.",
        "PasswordRequiresUpper" => "Пароль должен содержать хотя бы одну заглавную букву.",
        "PasswordRequiresNonAlphanumeric" => "Пароль должен содержать хотя бы один спецсимвол.",
        "InvalidEmail" => "Некорректный формат e-mail.",
        "InvalidUserName" => "Недопустимое имя пользователя.",
        _ => error.Description
    };
}
