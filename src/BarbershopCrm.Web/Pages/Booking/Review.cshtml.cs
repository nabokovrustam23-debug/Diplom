using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

/// <summary>
/// Форма отзыва клиента о выполненной услуге. Доступна владельцу записи только
/// после статуса Completed; одну запись — один отзыв (UNIQUE по BookingId).
/// </summary>
[Authorize]
public class ReviewModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReviewModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)] public int BookingId { get; set; }
    [BindProperty] public ReviewInput Input { get; set; } = new();
    public Domain.Entities.Booking? Booking { get; private set; }
    public bool AlreadyReviewed { get; private set; }

    [TempData] public string? StatusMessage { get; set; }

    public class ReviewInput
    {
        [Range(1, 5, ErrorMessage = "Поставьте оценку от 1 до 5.")]
        public int Rating { get; set; } = 5;
        [StringLength(2000, ErrorMessage = "Комментарий не должен быть длиннее 2000 символов.")]
        public string? Comment { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return RedirectToPage("/Index");
        if (AlreadyReviewed) return Page();

        if (!ModelState.IsValid) return Page();

        _db.Reviews.Add(new Review
        {
            BookingId = BookingId,
            Rating = Input.Rating,
            Comment = string.IsNullOrWhiteSpace(Input.Comment) ? null : Input.Comment.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        StatusMessage = "Спасибо за отзыв!";
        return RedirectToPage(new { bookingId = BookingId });
    }

    private async Task<bool> LoadAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return false;

        Booking = await _db.Bookings.AsNoTracking()
            .Include(b => b.Service)
            .Include(b => b.Master).ThenInclude(m => m.Persona)
            .Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.BookingId == BookingId);

        if (Booking is null) return false;
        // Только владелец Booking может оставить отзыв.
        if (Booking.Client.PersonaId != user.PersonaId) return false;
        // Только после Completed.
        if (Booking.Status != BookingStatus.Completed) return false;

        AlreadyReviewed = await _db.Reviews.AnyAsync(r => r.BookingId == BookingId);
        return true;
    }
}
