using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Schedules;

/// <summary>
/// Админская сводка по расписаниям мастеров: календарная сетка
/// (мастер × день недели) для выбранной недели + сводка исключений
/// на ближайшие 60 дней. Точечная правка смен — в карточке мастера
/// (/Staff/Schedule).
/// </summary>
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public DateOnly? WeekStart { get; set; }

    public DateOnly EffectiveWeekStart { get; private set; }

    public IList<MasterRow> Rows { get; private set; } = new List<MasterRow>();
    public IList<CalendarRow> Calendar { get; private set; } = new List<CalendarRow>();
    public IList<DateOnly> WeekDays { get; private set; } = new List<DateOnly>();

    public record MasterRow(
        int MasterId,
        string FullName,
        string BranchName,
        int VacationCount,
        int SickCount,
        int DayOffCount,
        DateOnly? NextAbsence);

    /// <summary>Строка календаря: мастер + 7 ячеек (пн..вс).</summary>
    public record CalendarRow(int MasterId, string FullName, string BranchName, CalendarCell[] Days);

    public record CalendarCell(DateOnly Date, CellKind Kind, string? Label);

    public enum CellKind
    {
        None,
        Work,
        DayOff,
        Vacation,
        SickLeave
    }

    public async Task OnGetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        // По умолчанию — текущая календарная неделя (с понедельника).
        EffectiveWeekStart = WeekStart
            ?? today.AddDays(-((int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1));
        WeekDays = Enumerable.Range(0, 7).Select(i => EffectiveWeekStart.AddDays(i)).ToList();

        var horizon = today.AddDays(60);

        var masters = await _db.Masters
            .AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.IsActive)
            .OrderBy(m => m.Persona.LastName)
            .ToListAsync();

        var futureSchedules = await _db.WorkSchedules
            .AsNoTracking()
            .Include(w => w.Branch)
            .Where(w => w.WorkDate >= today && w.WorkDate <= horizon
                && w.ScheduleType != ScheduleType.Work && w.ScheduleType != ScheduleType.Lunch)
            .ToListAsync();

        var branchByMaster = await _db.MasterBranches
            .AsNoTracking()
            .Include(mb => mb.Branch)
            .ToDictionaryAsync(mb => mb.MasterId, mb => mb.Branch.Name);

        Rows = masters.Select(m =>
        {
            var mine = futureSchedules.Where(s => s.MasterId == m.MasterId).ToList();
            return new MasterRow(
                m.MasterId,
                $"{m.Persona.LastName} {m.Persona.FirstName}",
                branchByMaster.TryGetValue(m.MasterId, out var b) ? b : "—",
                mine.Count(x => x.ScheduleType == ScheduleType.Vacation),
                mine.Count(x => x.ScheduleType == ScheduleType.SickLeave),
                mine.Count(x => x.ScheduleType == ScheduleType.DayOff),
                mine.OrderBy(x => x.WorkDate).FirstOrDefault()?.WorkDate);
        }).ToList();

        // Календарная сетка: берём все записи расписания на неделю.
        var weekEnd = EffectiveWeekStart.AddDays(6);
        var weekSchedules = await _db.WorkSchedules
            .AsNoTracking()
            .Where(w => w.WorkDate >= EffectiveWeekStart && w.WorkDate <= weekEnd)
            .ToListAsync();

        Calendar = masters.Select(m =>
        {
            var mine = weekSchedules.Where(s => s.MasterId == m.MasterId).ToList();
            var cells = WeekDays.Select(d =>
            {
                var sched = mine.Where(s => s.WorkDate == d).ToList();
                // Приоритет: отпуск/больничный/выходной → работа.
                var exc = sched.FirstOrDefault(s => s.ScheduleType == ScheduleType.Vacation)
                       ?? sched.FirstOrDefault(s => s.ScheduleType == ScheduleType.SickLeave)
                       ?? sched.FirstOrDefault(s => s.ScheduleType == ScheduleType.DayOff);
                if (exc is not null)
                {
                    var kind = exc.ScheduleType switch
                    {
                        ScheduleType.Vacation => CellKind.Vacation,
                        ScheduleType.SickLeave => CellKind.SickLeave,
                        _ => CellKind.DayOff
                    };
                    return new CalendarCell(d, kind, null);
                }
                var work = sched.FirstOrDefault(s => s.ScheduleType == ScheduleType.Work);
                if (work is not null)
                {
                    var label = $"{work.StartTime:HH\\:mm}–{work.EndTime:HH\\:mm}";
                    return new CalendarCell(d, CellKind.Work, label);
                }
                return new CalendarCell(d, CellKind.None, null);
            }).ToArray();

            return new CalendarRow(
                m.MasterId,
                $"{m.Persona.LastName} {m.Persona.FirstName}",
                branchByMaster.TryGetValue(m.MasterId, out var b) ? b : "—",
                cells);
        }).ToList();
    }
}
