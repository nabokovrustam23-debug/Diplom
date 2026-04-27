using System.Text.RegularExpressions;
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

    /// <summary>Колонка сортировки: name (по умолчанию), visits, lastVisit.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    /// <summary>Направление сортировки: asc (по умолчанию) или desc.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Dir { get; set; }

    public IList<ClientRow> Rows { get; private set; } = new List<ClientRow>();

    /// <summary>Возвращает «другое» направление для кликов по колонке.</summary>
    public string ToggleDir(string column) =>
        (Sort ?? "name") == column && (Dir ?? "asc") == "asc" ? "desc" : "asc";

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
            // Для телефонного поиска нормализуем и запрос, и поле: убираем всё
            // кроме цифр, чтобы «79201112233», «9201112233» и «+7 920 111 22 33»
            // давали один и тот же результат. Для коротких цифровых вводов
            // (< 4 цифр) пропускаем, чтобы не матчить половину базы.
            var digits = Regex.Replace(qt, "\\D", "");
            if (digits.Length >= 4)
            {
                // EF не переварит Regex — нормализуем через Replace на сервере.
                query = query.Where(c =>
                    c.Persona.LastName.Contains(qt)
                    || c.Persona.FirstName.Contains(qt)
                    || c.Persona.Phone
                        .Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "")
                        .Contains(digits)
                    || (c.Persona.Email != null && c.Persona.Email.Contains(qt)));
            }
            else
            {
                query = query.Where(c =>
                    c.Persona.LastName.Contains(qt)
                    || c.Persona.FirstName.Contains(qt)
                    || c.Persona.Phone.Contains(qt)
                    || (c.Persona.Email != null && c.Persona.Email.Contains(qt)));
            }
        }

        var list = await query
            .OrderBy(c => c.Persona.LastName)
            .ThenBy(c => c.Persona.FirstName)
            .ToListAsync();

        var rows = list.Select(c =>
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
        }).AsEnumerable();

        // Сортировка по выбранной колонке (визиты/последний визит/ФИО).
        var desc = (Dir ?? "asc") == "desc";
        rows = (Sort ?? "name") switch
        {
            "visits" => desc ? rows.OrderByDescending(r => r.TotalVisits) : rows.OrderBy(r => r.TotalVisits),
            "lastVisit" => desc
                ? rows.OrderByDescending(r => r.LastVisitAt ?? DateTime.MinValue)
                : rows.OrderBy(r => r.LastVisitAt ?? DateTime.MaxValue),
            _ => desc ? rows.OrderByDescending(r => r.FullName) : rows.OrderBy(r => r.FullName),
        };
        Rows = rows.ToList();
    }
}
