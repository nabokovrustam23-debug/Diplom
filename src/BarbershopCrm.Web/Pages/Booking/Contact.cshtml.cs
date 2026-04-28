using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using BarbershopCrm.Infrastructure.Services;
using BarbershopCrm.Web.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BarbershopCrm.Web.Pages.Booking;

[EnableRateLimiting("booking")]
public class ContactModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISlotService _slots;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly BookingPolicyOptions _policy;
    private readonly ILogger<ContactModel> _log;

    public ContactModel(ApplicationDbContext db, ISlotService slots, UserManager<ApplicationUser> userManager,
        IOptions<BookingPolicyOptions> policy, ILogger<ContactModel> log)
    {
        _db = db;
        _slots = slots;
        _userManager = userManager;
        _policy = policy.Value;
        _log = log;
    }

    /// <summary>
    /// Готовые пожелания, доступные клиенту чекбоксами на форме записи. Выбранные пункты
    /// сохраняются в Booking.Wishes как «;»-разделённый список (ровно те же ключи).
    /// </summary>
    public static readonly IReadOnlyList<string> WishOptions = new[]
    {
        "Беру с собой ребёнка",
        "Прошу не разговаривать",
        "Кофе/чай во время визита",
        "Нужна машинка №1",
        "Нужна машинка №2",
        "Сделать фото для соцсетей",
        "Парфюм после стрижки",
        "Доплачу безналично"
    };

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ServiceId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int MasterId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime Start { get; set; }

    [BindProperty]
    public ContactInput Input { get; set; } = new();

    /// <summary>
    /// Ключ идемпотентности: генерируется на GET и передаётся в скрытом поле.
    /// При повторном POST (двойной клик/refresh) одна и та же запись не создаётся
    /// дважды — благодаря уникальному индексу IdempotencyKey в БД.
    /// </summary>
    [BindProperty]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Текст вопроса капчи («Сколько будет 3 + 5?»).</summary>
    public string CaptchaQuestion { get; private set; } = string.Empty;
    /// <summary>Подписанный ответ — генерируется на GET и проверяется на POST.</summary>
    [BindProperty(Name = SimpleCaptcha.TokenField)]
    public string CaptchaToken { get; set; } = string.Empty;
    [BindProperty(Name = SimpleCaptcha.AnswerField)]
    public string? CaptchaAnswer { get; set; }
    /// <summary>Honeypot-поле: невидимо в браузере, заполняется только ботами.</summary>
    [BindProperty(Name = SimpleCaptcha.HoneypotField)]
    public string? Honeypot { get; set; }

    public Branch? Branch { get; private set; }
    public Domain.Entities.Service? Service { get; private set; }
    public Domain.Entities.Master? Master { get; private set; }

    /// <summary>
    /// Подсказка «ближайшее доступное» — заполняется, когда выбранный слот
    /// уже занят: показывается клиенту вместе с дружелюбным сообщением и
    /// ссылкой назад на экран выбора.
    /// </summary>
    public (DateOnly Date, TimeOnly Time)? SuggestedSlot { get; private set; }

    public class ContactInput
    {
        [Required(ErrorMessage = "Укажите фамилию.")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите имя.")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите телефон.")]
        [Phone(ErrorMessage = "Неверный формат телефона.")]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Неверный формат e-mail.")]
        [StringLength(256)]
        public string? Email { get; set; }

        [StringLength(500, ErrorMessage = "Комментарий не должен быть длиннее 500 символов.")]
        public string? Notes { get; set; }

        public List<string> Wishes { get; set; } = new();

        /// <summary>Согласие на обработку персональных данных (152-ФЗ). Обязательно
        /// для гостей; для авторизованных клиентов согласие подразумевается из регистрации,
        /// но мы всё равно требуем явный чекбокс — это упрощает аудит.</summary>
        [Display(Name = "Согласие на обработку персональных данных")]
        public bool ConsentGiven { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadContextAsync())
        {
            return RedirectToPage("Index");
        }

        // Свежий ключ идемпотентности для этой формы записи.
        IdempotencyKey = Guid.NewGuid().ToString("N");

        // Генерируем простую капчу (вопрос + подписанный токен ответа).
        var (a, b, token, q) = SimpleCaptcha.New();
        _ = a; _ = b;
        CaptchaQuestion = q;
        CaptchaToken = token;

        // Если пользователь залогинен — подставим его контактные данные из Persona.
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is not null)
            {
                var persona = await _db.Personas.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PersonaId == user.PersonaId);
                if (persona is not null)
                {
                    Input.LastName = persona.LastName;
                    Input.FirstName = persona.FirstName;
                    Input.Phone = persona.Phone;
                    Input.Email = persona.Email ?? user.Email;
                }
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadContextAsync())
        {
            return RedirectToPage("Index");
        }

        // Согласие на обработку ПДн обязательно при бронировании.
        if (!Input.ConsentGiven)
        {
            ModelState.AddModelError("Input.ConsentGiven",
                "Чтобы записаться, поставьте галочку согласия на обработку персональных данных.");
        }

        // Капча: honeypot должен быть пустым.
        if (!SimpleCaptcha.HoneypotIsClean(Honeypot))
        {
            _log.LogWarning("Booking honeypot triggered from {IP}", HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError(string.Empty,
                "Не удалось проверить форму. Перезагрузите страницу и попробуйте снова.");
        }
        else if (!SimpleCaptcha.Verify(CaptchaAnswer, CaptchaToken))
        {
            ModelState.AddModelError(SimpleCaptcha.AnswerField,
                "Капча не пройдена. Попробуйте ещё раз.");
        }

        // Проверка горизонта бронирования: запрещаем выбирать дату дальше N дней вперёд.
        var horizonEnd = DateTime.UtcNow.AddDays(_policy.HorizonDays);
        if (Start > horizonEnd)
        {
            ModelState.AddModelError(string.Empty,
                $"Запись возможна не дальше, чем на {_policy.HorizonDays} дней вперёд.");
        }

        if (!ModelState.IsValid)
        {
            // При ошибке валидации перегенерируем капчу — старый токен уже использован.
            var (_, _, token, q) = SimpleCaptcha.New();
            CaptchaQuestion = q;
            CaptchaToken = token;
            return Page();
        }

        // Идемпотентность: если пришёл повторный POST с тем же ключом —
        // не создаём дубликат, а просто возвращаем уже созданную запись.
        if (Guid.TryParse(IdempotencyKey, out var idemGuid))
        {
            var existing = await _db.Bookings.AsNoTracking()
                .FirstOrDefaultAsync(b => b.IdempotencyKey == idemGuid);
            if (existing is not null)
            {
                return RedirectToPage("Success", new { bookingId = existing.BookingId });
            }
        }
        else
        {
            idemGuid = Guid.NewGuid();
        }

        // Повторная проверка слота — на случай гонки.
        var date = DateOnly.FromDateTime(Start);
        var time = TimeOnly.FromDateTime(Start);
        var available = await _slots.GetAvailableSlotsAsync(MasterId, BranchId, ServiceId, date);

        if (!available.Contains(time))
        {
            await ShowSlotTakenAsync();
            return Page();
        }

        // Нормализация телефона через единый утилитный метод.
        // Это гарантирует, что номер вида «8 999 …» и «+7 999 …» сводятся
        // к одной канонической записи и работает поиск Persona по уникальному индексу.
        var phone = PhoneUtil.Normalize(Input.Phone);

        // Транзакция нужна, чтобы создание Persona/Client/Booking
        // и проверка занятости слота прошли как единое целое.
        // Уникальный частичный индекс UX_Bookings_MasterStart_Active
        // на уровне БД отказывает в SaveChanges, если параллельный POST
        // успел создать запись на тот же слот первым.
        await using var tx = await _db.Database.BeginTransactionAsync();

        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.Phone == phone);
        if (persona is null)
        {
            persona = new Persona
            {
                LastName = Input.LastName.Trim(),
                FirstName = Input.FirstName.Trim(),
                Phone = phone,
                Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim()
            };
            _db.Personas.Add(persona);
            await _db.SaveChangesAsync();
        }

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.PersonaId == persona.PersonaId);
        if (client is null)
        {
            client = new Client
            {
                PersonaId = persona.PersonaId,
                Source = "online",
                FirstVisitDate = DateOnly.FromDateTime(Start),
                CreatedAt = DateTime.UtcNow
            };
            _db.Clients.Add(client);
            await _db.SaveChangesAsync();
        }

        // Сохраняем выбранные пожелания только из официального списка (защита от подделки).
        var allowedWishes = Input.Wishes
            .Where(w => WishOptions.Contains(w))
            .Distinct()
            .ToList();
        var wishes = allowedWishes.Count > 0 ? string.Join(";", allowedWishes) : null;

        var booking = new Domain.Entities.Booking
        {
            ClientId = client.ClientId,
            MasterId = MasterId,
            ServiceId = ServiceId,
            BranchId = BranchId,
            // Время в БД храним как UTC: явно фиксируем Kind, чтобы EF
            // не «угадывал» при сравнении с DateTime.UtcNow.
            StartDateTime = DateTime.SpecifyKind(Start, DateTimeKind.Utc),
            DurationMinutes = Service!.DurationMinutes,
            Status = BookingStatus.Created,
            CreatedAt = DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes.Trim(),
            Wishes = wishes,
            IdempotencyKey = idemGuid
        };
        _db.Bookings.Add(booking);

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

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Конфликт уникального индекса — слот уже занят либо повторный POST
            // выиграл гонку. Откатываем и показываем дружелюбную ошибку.
            await tx.RollbackAsync();
            await ShowSlotTakenAsync();
            return Page();
        }

        await tx.CommitAsync();

        return RedirectToPage("Success", new { bookingId = booking.BookingId });
    }

    private async Task ShowSlotTakenAsync()
    {
        SuggestedSlot = await _slots.GetNextAvailableSlotAsync(MasterId, BranchId, ServiceId, horizonDays: SlotModel.HorizonDays);
        var suggestion = SuggestedSlot is null
            ? "К сожалению, свободных слотов на ближайшие две недели не осталось."
            : $"Ближайшее свободное окно — {SuggestedSlot.Value.Date:dd.MM} в {SuggestedSlot.Value.Time:HH\\:mm}.";
        ModelState.AddModelError(string.Empty,
            $"Ой, этот слот уже заняли. {suggestion} Вернитесь на шаг «Время» и выберите другое.");
    }

    private async Task<bool> LoadContextAsync()
    {
        Branch = await _db.Branches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BranchId == BranchId);
        Service = await _db.Services.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceId == ServiceId);
        Master = await _db.Masters.AsNoTracking()
            .Include(m => m.Persona)
            .FirstOrDefaultAsync(m => m.MasterId == MasterId);

        return Branch is not null && Service is not null && Master is not null && Start != default;
    }
}
