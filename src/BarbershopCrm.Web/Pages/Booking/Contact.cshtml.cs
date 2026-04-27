using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

public class ContactModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISlotService _slots;

    public ContactModel(ApplicationDbContext db, ISlotService slots)
    {
        _db = db;
        _slots = slots;
    }

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

    public Branch? Branch { get; private set; }
    public Domain.Entities.Service? Service { get; private set; }
    public Master? Master { get; private set; }

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
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadContextAsync())
        {
            return RedirectToPage("Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadContextAsync())
        {
            return RedirectToPage("Index");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Повторная проверка слота — на случай гонки.
        var date = DateOnly.FromDateTime(Start);
        var time = TimeOnly.FromDateTime(Start);
        var available = await _slots.GetAvailableSlotsAsync(MasterId, BranchId, ServiceId, date);

        if (!available.Contains(time))
        {
            ModelState.AddModelError(string.Empty, "Выбранный слот уже занят. Пожалуйста, выберите другое время.");
            return Page();
        }

        // Поиск/создание Persona по нормализованному телефону.
        var phone = NormalizePhone(Input.Phone);
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

        // Поиск/создание Client.
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

        var booking = new Domain.Entities.Booking
        {
            ClientId = client.ClientId,
            MasterId = MasterId,
            ServiceId = ServiceId,
            BranchId = BranchId,
            StartDateTime = Start,
            DurationMinutes = Service!.DurationMinutes,
            Status = BookingStatus.Created,
            CreatedAt = DateTime.UtcNow
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        return RedirectToPage("Success", new { bookingId = booking.BookingId });
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

    private static string NormalizePhone(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("8") && digits.Length == 11)
        {
            digits = "7" + digits[1..];
        }
        return "+" + digits;
    }
}
