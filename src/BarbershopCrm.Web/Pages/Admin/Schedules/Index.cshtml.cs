using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Schedules;

/// <summary>
/// Админская сводка по расписаниям мастеров: активные исключения
/// (отпуск/больничный/выходной), рядом — ссылка на календарную сетку
/// конкретного мастера (/Staff/Schedule). Полный разбор часов и
/// добавление/снятие исключений выполняется в карточке мастера.
/// </summary>
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public IList<MasterRow> Rows { get; private set; } = new List<MasterRow>();

    public record MasterRow(
        int MasterId,
        string FullName,
        string BranchName,
        int VacationCount,
        int SickCount,
        int DayOffCount,
        DateOnly? NextAbsence);

    public async Task OnGetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var horizon = today.AddDays(60);

        var masters = await _db.Masters
            .AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.IsActive)
            .OrderBy(m => m.Persona.LastName)
            .ToListAsync();

        var schedules = await _db.WorkSchedules
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
            var mine = schedules.Where(s => s.MasterId == m.MasterId).ToList();
            return new MasterRow(
                m.MasterId,
                $"{m.Persona.LastName} {m.Persona.FirstName}",
                branchByMaster.TryGetValue(m.MasterId, out var b) ? b : "—",
                mine.Count(x => x.ScheduleType == ScheduleType.Vacation),
                mine.Count(x => x.ScheduleType == ScheduleType.SickLeave),
                mine.Count(x => x.ScheduleType == ScheduleType.DayOff),
                mine.OrderBy(x => x.WorkDate).FirstOrDefault()?.WorkDate);
        }).ToList();
    }
}
