using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Clients;

/// <summary>
/// Карточка клиента: сводка, история посещений (все бронирования),
/// редактируемые CRM-заметки (Client.Notes). Повторный клиент определяется
/// по количеству завершённых визитов.
/// </summary>
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DetailsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Client? Client { get; private set; }
    public IList<Domain.Entities.Booking> Bookings { get; private set; } = new List<Domain.Entities.Booking>();

    [BindProperty]
    public string? Notes { get; set; }

    [BindProperty]
    public string? Source { get; set; }

    /// <summary>Предустановленные источники привлечения + «Другое».</summary>
    public static readonly IReadOnlyList<string> SourceOptions = new[]
    {
        "Инстаграм",
        "Рекомендация",
        "Прохожий",
        "Сайт",
        "Другое"
    };

    [TempData] public string? StatusMessage { get; set; }

    public int CompletedCount => Bookings.Count(b => b.Status == BookingStatus.Completed);
    public int CancelledCount => Bookings.Count(b => b.Status == BookingStatus.Cancelled);
    public int NoShowCount => Bookings.Count(b => b.Status == BookingStatus.NoShow);
    public bool IsRepeat => CompletedCount >= 2;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return RedirectToPage("Index");
        Notes = Client!.Notes;
        Source = Client!.Source;
        return Page();
    }

    public async Task<IActionResult> OnPostSaveNotesAsync()
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.ClientId == Id);
        if (client is null) return RedirectToPage("Index");

        client.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
        client.Source = string.IsNullOrWhiteSpace(Source) ? null : Source.Trim();
        await _db.SaveChangesAsync();

        StatusMessage = "Карточка сохранена.";
        return RedirectToPage(new { Id });
    }

    private async Task<bool> LoadAsync()
    {
        Client = await _db.Clients
            .AsNoTracking()
            .Include(c => c.Persona)
            .FirstOrDefaultAsync(c => c.ClientId == Id);

        if (Client is null) return false;

        Bookings = await _db.Bookings
            .AsNoTracking()
            .Include(b => b.Branch)
            .Include(b => b.Service)
            .Include(b => b.Master).ThenInclude(m => m.Persona)
            .Where(b => b.ClientId == Id)
            .OrderByDescending(b => b.StartDateTime)
            .ToListAsync();

        return true;
    }
}
