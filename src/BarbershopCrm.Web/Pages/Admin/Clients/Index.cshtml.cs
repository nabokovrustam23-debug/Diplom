using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Clients;

/// <summary>
/// Список клиентов сети с поиском по ФИО/телефону/email. Для каждого клиента
/// агрегируются визиты и автоматически проставляется признак «повторный»
/// (≥ 2 завершённых записей), что делает ценность CRM-функции видимой.
/// </summary>
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public IList<ClientRow> Rows { get; private set; } = new List<ClientRow>();

    public record ClientRow(
        int ClientId,
        string FullName,
        string Phone,
        string? Email,
        string? Source,
        DateOnly? FirstVisit,
        int TotalVisits,
        int CompletedVisits,
        bool IsRepeat,
        DateTime? LastVisitAt);

    public async Task OnGetAsync()
    {
        var query = _db.Clients
            .AsNoTracking()
            .Include(c => c.Persona)
            .Include(c => c.Bookings)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var qt = Q.Trim();
            query = query.Where(c =>
                c.Persona.LastName.Contains(qt)
                || c.Persona.FirstName.Contains(qt)
                || c.Persona.Phone.Contains(qt)
                || (c.Persona.Email != null && c.Persona.Email.Contains(qt)));
        }

        var list = await query
            .OrderBy(c => c.Persona.LastName)
            .ThenBy(c => c.Persona.FirstName)
            .ToListAsync();

        Rows = list.Select(c =>
        {
            var completed = c.Bookings.Count(b => b.Status == BookingStatus.Completed);
            return new ClientRow(
                c.ClientId,
                $"{c.Persona.LastName} {c.Persona.FirstName}".Trim(),
                c.Persona.Phone,
                c.Persona.Email,
                c.Source,
                c.FirstVisitDate,
                c.Bookings.Count,
                completed,
                completed >= 2,
                c.Bookings.OrderByDescending(b => b.StartDateTime).FirstOrDefault()?.StartDateTime);
        }).ToList();
    }
}
