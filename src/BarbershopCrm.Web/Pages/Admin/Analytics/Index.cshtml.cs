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

    public IList<BranchRow> BranchRows { get; private set; } = new List<BranchRow>();
    public IList<MasterRow> MasterRows { get; private set; } = new List<MasterRow>();
    public IList<ServiceRow> ServiceRows { get; private set; } = new List<ServiceRow>();

    public record BranchRow(int BranchId, string Name, int Total, int Completed, decimal Revenue, int NoShow);
    public record MasterRow(int MasterId, string FullName, string BranchName, int Completed, decimal Revenue, int WorkingMinutes, int BookedMinutes, double UtilizationPercent);
    public record ServiceRow(string Name, int Completed, decimal Revenue);

    public async Task OnGetAsync()
    {
        Branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
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

        TotalRevenue = bookings.Where(b => b.Status == BookingStatus.Completed)
            .Sum(b => b.Service.Price);

        NoShowRate = FunnelTotal > 0 ? 100.0 * FunnelNoShow / FunnelTotal : 0;
        CancelRate = FunnelTotal > 0 ? 100.0 * FunnelCancelled / FunnelTotal : 0;

        var uniqueClientIds = bookings.Select(b => b.ClientId).Distinct().ToList();
        UniqueClients = uniqueClientIds.Count;

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
            .ToDictionaryAsync(mb => mb.MasterId, mb => mb.Branch.Name);

        MasterRows = bookings
            .GroupBy(b => new { b.MasterId, FullName = $"{b.Master.Persona.LastName} {b.Master.Persona.FirstName}" })
            .Select(g =>
            {
                var booked = g.Where(x => x.Status == BookingStatus.Completed || x.Status == BookingStatus.Confirmed)
                    .Sum(x => x.DurationMinutes);
                var working = workingByMaster.TryGetValue(g.Key.MasterId, out var w) ? w : 0;
                var util = working > 0 ? Math.Round(100.0 * booked / working, 1) : 0;
                var branch = branchByMaster.TryGetValue(g.Key.MasterId, out var b) ? b : "—";
                return new MasterRow(
                    g.Key.MasterId,
                    g.Key.FullName,
                    branch,
                    g.Count(x => x.Status == BookingStatus.Completed),
                    g.Where(x => x.Status == BookingStatus.Completed).Sum(x => x.Service.Price),
                    working,
                    booked,
                    util);
            })
            .OrderByDescending(r => r.UtilizationPercent)
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
