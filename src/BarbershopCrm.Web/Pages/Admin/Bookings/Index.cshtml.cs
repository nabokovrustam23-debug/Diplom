using System.Globalization;
using System.Text;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BookingEntity = BarbershopCrm.Domain.Entities.Booking;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Web.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Bookings;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<BookingEntity> Bookings { get; private set; } = new List<BookingEntity>();
    public IList<Branch> Branches { get; private set; } = new List<Branch>();
    public IList<Master> Masters { get; private set; } = new List<Master>();

    /// <summary>
    /// ClientId → признак «повторный» (≥ 2 завершённых визита за всё время).
    /// Считается один раз и используется в шаблоне для показа значка «повторный».
    /// </summary>
    public IReadOnlyDictionary<int, bool> RepeatClients { get; private set; }
        = new Dictionary<int, bool>();

    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? DateFilter { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public int? BranchId { get; set; }
    [BindProperty(SupportsGet = true)] public int? MasterId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public async Task OnGetAsync()
    {
        Branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
        Masters = await _db.Masters.AsNoTracking()
            .Include(m => m.Persona)
            .OrderBy(m => m.Persona.LastName)
            .ToListAsync();

        var q = _db.Bookings
            .Include(b => b.Branch)
            .Include(b => b.Service)
            .Include(b => b.Master).ThenInclude(m => m.Persona)
            .Include(b => b.Client).ThenInclude(c => c.Persona)
            .AsNoTracking()
            .AsQueryable();

        if (Enum.TryParse<BookingStatus>(StatusFilter, out var s))
            q = q.Where(b => b.Status == s);

        // Быстрые пресеты по дате имеют приоритет над пользовательским диапазоном.
        var today = DateTime.UtcNow.Date;
        switch (DateFilter)
        {
            case "today":
                q = q.Where(b => b.StartDateTime >= today && b.StartDateTime < today.AddDays(1));
                break;
            case "tomorrow":
                q = q.Where(b => b.StartDateTime >= today.AddDays(1) && b.StartDateTime < today.AddDays(2));
                break;
            case "week":
                q = q.Where(b => b.StartDateTime >= today && b.StartDateTime < today.AddDays(7));
                break;
            case "past":
                q = q.Where(b => b.StartDateTime < today);
                break;
            default:
                if (FromDate.HasValue)
                    q = q.Where(b => b.StartDateTime >= FromDate.Value.ToDateTime(TimeOnly.MinValue));
                if (ToDate.HasValue)
                    q = q.Where(b => b.StartDateTime < ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
                break;
        }

        if (BranchId.HasValue && BranchId.Value > 0)
            q = q.Where(b => b.BranchId == BranchId.Value);

        if (MasterId.HasValue && MasterId.Value > 0)
            q = q.Where(b => b.MasterId == MasterId.Value);

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var needle = Q.Trim();
            q = q.Where(b =>
                b.Client.Persona.LastName.Contains(needle) ||
                b.Client.Persona.FirstName.Contains(needle) ||
                b.Client.Persona.Phone.Contains(needle));
        }

        Bookings = await q
            .OrderByDescending(b => b.StartDateTime)
            .Take(200)
            .ToListAsync();

        var clientIds = Bookings.Select(b => b.ClientId).Distinct().ToList();
        var completedCounts = await _db.Bookings
            .AsNoTracking()
            .Where(b => clientIds.Contains(b.ClientId) && b.Status == BookingStatus.Completed)
            .GroupBy(b => b.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToListAsync();
        RepeatClients = completedCounts.ToDictionary(x => x.ClientId, x => x.Count >= 2);
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, BookingStatus status)
    {
        var b = await _db.Bookings.FindAsync(id);
        if (b is not null)
        {
            var oldStatus = b.Status;
            b.Status = status;
            AuditLogger.Log(_db, User, "ChangeStatus", "Booking", b.BookingId.ToString(),
                $"from={oldStatus} to={status}");
            await _db.SaveChangesAsync();
        }
        return RedirectAfterMutation();
    }

    /// <summary>
    /// Массовое завершение записей: в конце дня администратор отмечает пачку
    /// подтверждённых визитов как Completed одним действием вместо построчных
    /// кликов. Работает только для записей в статусе Confirmed — Created/No-show
    /// сюда не попадают.
    /// </summary>
    public async Task<IActionResult> OnPostBulkCompleteAsync(int[] ids)
    {
        if (ids is { Length: > 0 })
        {
            var targets = await _db.Bookings
                .Where(b => ids.Contains(b.BookingId) && b.Status == BookingStatus.Confirmed)
                .ToListAsync();
            foreach (var b in targets)
            {
                b.Status = BookingStatus.Completed;
                AuditLogger.Log(_db, User, "BulkComplete", "Booking", b.BookingId.ToString(), null);
            }
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = $"Завершено записей: {targets.Count}.";
        }
        return RedirectAfterMutation();
    }

    /// <summary>
    /// Авто-маркировка no-show: все «Created»/«Confirmed» записи, начало
    /// которых уже более 30 минут в прошлом, переводятся в статус NoShow.
    /// Используется кнопкой «Закрыть смену» в журнале администратора и
    /// заменяет ручной обход просроченных записей.
    /// </summary>
    public async Task<IActionResult> OnPostMarkNoShowAsync()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-30);
        var stale = await _db.Bookings
            .Where(b => b.StartDateTime < threshold
                && (b.Status == BookingStatus.Created || b.Status == BookingStatus.Confirmed))
            .ToListAsync();

        foreach (var b in stale)
        {
            b.Status = BookingStatus.NoShow;
            AuditLogger.Log(_db, User, "AutoNoShow", "Booking", b.BookingId.ToString(),
                $"start={b.StartDateTime:yyyy-MM-dd HH:mm}");
        }
        await _db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Помечено как no-show: {stale.Count}.";
        return RedirectAfterMutation();
    }

    private IActionResult RedirectAfterMutation() => RedirectToPage(new
    {
        StatusFilter,
        DateFilter,
        FromDate = FromDate?.ToString("yyyy-MM-dd"),
        ToDate = ToDate?.ToString("yyyy-MM-dd"),
        BranchId,
        MasterId,
        Q
    });

    /// <summary>Экспорт текущей выборки записей в CSV (Excel-совместимый: BOM + ;).
    /// Применяет те же фильтры, что и страница, чтобы выгрузка совпадала с тем,
    /// что видит администратор.</summary>
    public async Task<IActionResult> OnGetExportAsync()
    {
        await OnGetAsync();

        var sb = new StringBuilder();
        var cul = CultureInfo.InvariantCulture;
        sb.AppendLine("Дата;Время;Филиал;Услуга;Мастер;Клиент;Телефон;Длительность мин;Цена;Статус");
        foreach (var b in Bookings)
        {
            var status = b.Status switch
            {
                BookingStatus.Created => "создана",
                BookingStatus.Confirmed => "подтверждена",
                BookingStatus.Completed => "завершена",
                BookingStatus.Cancelled => "отменена",
                BookingStatus.NoShow => "не пришёл",
                _ => b.Status.ToString()
            };
            sb.Append(b.StartDateTime.ToString("yyyy-MM-dd")).Append(';');
            sb.Append(b.StartDateTime.ToString("HH:mm")).Append(';');
            sb.Append(Csv(b.Branch?.Name ?? string.Empty)).Append(';');
            sb.Append(Csv(b.Service?.Name ?? string.Empty)).Append(';');
            sb.Append(Csv($"{b.Master?.Persona?.LastName} {b.Master?.Persona?.FirstName}".Trim())).Append(';');
            sb.Append(Csv($"{b.Client?.Persona?.LastName} {b.Client?.Persona?.FirstName}".Trim())).Append(';');
            sb.Append(Csv(b.Client?.Persona?.Phone ?? string.Empty)).Append(';');
            sb.Append(b.DurationMinutes).Append(';');
            sb.Append(b.Service?.Price.ToString(cul) ?? "0").Append(';');
            sb.AppendLine(status);
        }
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"bookings-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>Экранирование значения для CSV: оборачиваем в кавычки только
    /// при наличии разделителя/кавычек/переноса; внутренние кавычки удваиваем.</summary>
    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
