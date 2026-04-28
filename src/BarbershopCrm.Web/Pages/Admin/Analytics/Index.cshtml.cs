using System.Globalization;
using System.Text;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Analytics;

/// <summary>
/// Развёрнутая аналитика для владельца/администратора: воронка записей,
/// выручка и нагрузка по филиалам, загрузка мастеров, no-show / cancel /
/// повторные клиенты. Фильтры: период (сегодня/неделя/месяц/диапазон) и
/// филиал. Выводы считаются на лету из таблицы записей — отдельного хранилища
/// агрегатов нет.
/// </summary>
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public int? BranchId { get; set; }

    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly EffectiveTo { get; private set; }

    public IList<Branch> Branches { get; private set; } = new List<Branch>();

    // Воронка по статусам.
    public int FunnelCreated { get; private set; }
    public int FunnelConfirmed { get; private set; }
    public int FunnelCompleted { get; private set; }
    public int FunnelCancelled { get; private set; }
    public int FunnelNoShow { get; private set; }
    public int FunnelTotal => FunnelCreated + FunnelConfirmed + FunnelCompleted + FunnelCancelled + FunnelNoShow;

    public decimal TotalRevenue { get; private set; }
    public double NoShowRate { get; private set; }
    public double CancelRate { get; private set; }
    public int UniqueClients { get; private set; }
    public int RepeatClients { get; private set; }
    public double RepeatShare { get; private set; }
    public int NewClients { get; private set; }
    public decimal AvgCheck { get; private set; }
    public int AvgDurationMinutes { get; private set; }

    /// <summary>Предупреждение валидации (например, «кастом без дат»).</summary>
    public string? Warning { get; private set; }

    public IList<BranchRow> BranchRows { get; private set; } = new List<BranchRow>();
    public IList<MasterRow> MasterRows { get; private set; } = new List<MasterRow>();
    public IList<ServiceRow> ServiceRows { get; private set; } = new List<ServiceRow>();
    public IList<DailyPoint> DailyPoints { get; private set; } = new List<DailyPoint>();

    public record BranchRow(int BranchId, string Name, int Total, int Completed, decimal Revenue, int NoShow);
    public record MasterRow(int MasterId, string FullName, string BranchName, int Completed, decimal Revenue, int WorkingMinutes, int BookedMinutes, double UtilizationPercent);
    public record ServiceRow(string Name, int Completed, decimal Revenue);
    public record DailyPoint(DateOnly Date, int BookingsCount, decimal Revenue);

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    /// <summary>Экспорт текущей аналитики в CSV: KPI по периоду + разрезы
    /// по филиалам/мастерам. Удобен для сохранения снимка отчёта.</summary>
    public async Task<IActionResult> OnGetExportAsync()
    {
        await LoadAsync();
        var sb = new StringBuilder();
        var cul = CultureInfo.InvariantCulture;
        sb.AppendLine("Section;Key;Value");
        sb.AppendLine($"Period;From;{EffectiveFrom:yyyy-MM-dd}");
        sb.AppendLine($"Period;To;{EffectiveTo:yyyy-MM-dd}");
        sb.AppendLine($"KPI;TotalBookings;{FunnelTotal}");
        sb.AppendLine($"KPI;TotalRevenue;{TotalRevenue.ToString(cul)}");
        sb.AppendLine($"KPI;AvgCheck;{AvgCheck.ToString(cul)}");
        sb.AppendLine($"KPI;AvgDurationMinutes;{AvgDurationMinutes}");
        sb.AppendLine($"KPI;UniqueClients;{UniqueClients}");
        sb.AppendLine($"KPI;NewClients;{NewClients}");
        sb.AppendLine($"KPI;RepeatClients;{RepeatClients}");
        sb.AppendLine($"Funnel;Created;{FunnelCreated}");
        sb.AppendLine($"Funnel;Confirmed;{FunnelConfirmed}");
        sb.AppendLine($"Funnel;Completed;{FunnelCompleted}");
        sb.AppendLine($"Funnel;Cancelled;{FunnelCancelled}");
        sb.AppendLine($"Funnel;NoShow;{FunnelNoShow}");

        sb.AppendLine();
        sb.AppendLine("Branch;Name;Total;Completed;Revenue;NoShow");
        foreach (var r in BranchRows)
            sb.AppendLine($"{r.BranchId};{r.Name};{r.Total};{r.Completed};{r.Revenue.ToString(cul)};{r.NoShow}");

        sb.AppendLine();
        sb.AppendLine("Master;FullName;Branch;Completed;Revenue;WorkingMin;BookedMin;Utilization%");
        foreach (var r in MasterRows)
            sb.AppendLine($"{r.MasterId};{r.FullName};{r.BranchName};{r.Completed};{r.Revenue.ToString(cul)};{r.WorkingMinutes};{r.BookedMinutes};{r.UtilizationPercent.ToString(cul)}");

        sb.AppendLine();
        sb.AppendLine("Date;Bookings;Revenue");
        foreach (var p in DailyPoints)
            sb.AppendLine($"{p.Date:yyyy-MM-dd};{p.BookingsCount};{p.Revenue.ToString(cul)}");

        // BOM чтобы Excel сразу подхватывал UTF-8.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"analytics-{EffectiveFrom:yyyyMMdd}-{EffectiveTo:yyyyMMdd}.csv");
    }

    private async Task LoadAsync()
    {
        Branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (Period == "custom" && (From is null || To is null))
        {
            Warning = "Для произвольного периода заполните обе даты. Показан последний 7 дней.";
            Period = null;
        }
        if (Period == "custom" && From is not null && To is not null && From > To)
        {
            Warning = "Дата «с» позднее даты «по». Даты переставлены.";
            (From, To) = (To, From);
        }

        (EffectiveFrom, EffectiveTo) = Period switch
        {
            "today" => (today, today),
            "week" => (today.AddDays(-(int)(today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1)), today),
            "month" => (new DateOnly(today.Year, today.Month, 1), today),
            "custom" when From is not null && To is not null => (From.Value, To.Value),
            _ => (today.AddDays(-6), today),
        };

        var start = EffectiveFrom.ToDateTime(TimeOnly.MinValue);
        var endExclusive = EffectiveTo.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var bookingsQ = _db.Bookings
            .AsNoTracking()
            .Where(b => b.StartDateTime >= start && b.StartDateTime < endExclusive);

        if (BranchId is int bid and > 0) bookingsQ = bookingsQ.Where(b => b.BranchId == bid);

        var bookings = await bookingsQ
            .Include(b => b.Service)
            .Include(b => b.Branch)
            .Include(b => b.Master).ThenInclude(m => m.Persona)
            .ToListAsync();

        FunnelCreated = bookings.Count(b => b.Status == BookingStatus.Created);
        FunnelConfirmed = bookings.Count(b => b.Status == BookingStatus.Confirmed);
        FunnelCompleted = bookings.Count(b => b.Status == BookingStatus.Completed);
        FunnelCancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        FunnelNoShow = bookings.Count(b => b.Status == BookingStatus.NoShow);

        var completedBookings = bookings.Where(b => b.Status == BookingStatus.Completed).ToList();
        TotalRevenue = completedBookings.Sum(b => b.Service.Price);
        AvgCheck = completedBookings.Count > 0
            ? Math.Round(TotalRevenue / completedBookings.Count, 0)
            : 0m;
        AvgDurationMinutes = completedBookings.Count > 0
            ? (int)Math.Round(completedBookings.Average(b => b.DurationMinutes))
            : 0;

        NoShowRate = FunnelTotal > 0 ? 100.0 * FunnelNoShow / FunnelTotal : 0;
        CancelRate = FunnelTotal > 0 ? 100.0 * FunnelCancelled / FunnelTotal : 0;

        var uniqueClientIds = bookings.Select(b => b.ClientId).Distinct().ToList();
        UniqueClients = uniqueClientIds.Count;

        // Новые клиенты периода: у кого самая первая запись в сети попадает в период.
        if (uniqueClientIds.Count > 0)
        {
            var firstBookingByClient = await _db.Bookings
                .AsNoTracking()
                .Where(b => uniqueClientIds.Contains(b.ClientId))
                .GroupBy(b => b.ClientId)
                .Select(g => new { ClientId = g.Key, First = g.Min(x => x.StartDateTime) })
                .ToListAsync();
            NewClients = firstBookingByClient
                .Count(x => x.First >= start && x.First < endExclusive);
        }

        // Суточный разрез: сколько записей создано и сколько выручки завершённых визитов.
        var byDay = bookings.GroupBy(b => DateOnly.FromDateTime(b.StartDateTime))
            .ToDictionary(g => g.Key, g => g.ToList());
        var allDays = Enumerable.Range(0, EffectiveTo.DayNumber - EffectiveFrom.DayNumber + 1)
            .Select(i => EffectiveFrom.AddDays(i))
            .ToList();
        DailyPoints = allDays
            .Select(d =>
            {
                var day = byDay.TryGetValue(d, out var list) ? list : new List<Domain.Entities.Booking>();
                return new DailyPoint(
                    d,
                    day.Count,
                    day.Where(x => x.Status == BookingStatus.Completed).Sum(x => x.Service.Price));
            })
            .ToList();

        // «Повторный» считаем как клиента с ≥ 2 завершёнными визитами за всё время
        // (чтобы в маленькой витрине выборки не потерять полезный показатель).
        if (uniqueClientIds.Count > 0)
        {
            var completedCounts = await _db.Bookings
                .AsNoTracking()
                .Where(b => uniqueClientIds.Contains(b.ClientId) && b.Status == BookingStatus.Completed)
                .GroupBy(b => b.ClientId)
                .Select(g => new { Cid = g.Key, Count = g.Count() })
                .ToListAsync();
            RepeatClients = completedCounts.Count(x => x.Count >= 2);
            RepeatShare = UniqueClients > 0 ? 100.0 * RepeatClients / UniqueClients : 0;
        }

        BranchRows = bookings
            .GroupBy(b => new { b.BranchId, b.Branch.Name })
            .Select(g => new BranchRow(
                g.Key.BranchId,
                g.Key.Name,
                g.Count(),
                g.Count(x => x.Status == BookingStatus.Completed),
                g.Where(x => x.Status == BookingStatus.Completed).Sum(x => x.Service.Price),
                g.Count(x => x.Status == BookingStatus.NoShow)))
            .OrderByDescending(r => r.Revenue)
            .ToList();

        // Загрузка мастера: суммарная длительность завершённых+подтверждённых записей,
        // делённая на общее рабочее время мастера (из WorkSchedule.Work)
        // в выбранном интервале.
        var schedules = await _db.WorkSchedules
            .AsNoTracking()
            .Where(w => w.WorkDate >= EffectiveFrom && w.WorkDate <= EffectiveTo
                && w.ScheduleType == ScheduleType.Work)
            .ToListAsync();
        var workingByMaster = schedules
            .GroupBy(s => s.MasterId)
            .ToDictionary(
                g => g.Key,
                g => (int)g.Sum(s => (s.EndTime - s.StartTime).TotalMinutes));

        var branchByMaster = await _db.MasterBranches
            .AsNoTracking()
            .Include(mb => mb.Branch)
            .Where(mb => BranchId == null || BranchId == 0 || mb.BranchId == BranchId)
            .ToDictionaryAsync(mb => mb.MasterId, mb => mb.Branch.Name);

        // Стартуем от списка активных мастеров (с учётом фильтра филиала),
        // чтобы простаивавшие в период тоже отображались с 0% и были видны.
        var allMasters = await _db.Masters
            .AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.IsActive && (BranchId == null || BranchId == 0 || branchByMaster.Keys.Contains(m.MasterId)))
            .ToListAsync();

        var bookingsByMaster = bookings
            .GroupBy(b => b.MasterId)
            .ToDictionary(g => g.Key, g => g.ToList());

        MasterRows = allMasters
            .Select(m =>
            {
                var mb = bookingsByMaster.TryGetValue(m.MasterId, out var list) ? list : new List<Domain.Entities.Booking>();
                var booked = mb.Where(x => x.Status == BookingStatus.Completed || x.Status == BookingStatus.Confirmed)
                    .Sum(x => x.DurationMinutes);
                var working = workingByMaster.TryGetValue(m.MasterId, out var w) ? w : 0;
                var util = working > 0 ? Math.Round(100.0 * booked / working, 1) : 0;
                var branch = branchByMaster.TryGetValue(m.MasterId, out var bn) ? bn : "—";
                return new MasterRow(
                    m.MasterId,
                    $"{m.Persona.LastName} {m.Persona.FirstName}",
                    branch,
                    mb.Count(x => x.Status == BookingStatus.Completed),
                    mb.Where(x => x.Status == BookingStatus.Completed).Sum(x => x.Service.Price),
                    working,
                    booked,
                    util);
            })
            .OrderByDescending(r => r.UtilizationPercent)
            .ThenBy(r => r.FullName)
            .ToList();

        ServiceRows = bookings
            .Where(b => b.Status == BookingStatus.Completed)
            .GroupBy(b => b.Service.Name)
            .Select(g => new ServiceRow(g.Key, g.Count(), g.Sum(x => x.Service.Price)))
            .OrderByDescending(r => r.Revenue)
            .Take(10)
            .ToList();
    }
}
