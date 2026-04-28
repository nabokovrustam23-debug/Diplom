using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Infrastructure.Services;

/// <summary>
/// Реализация <see cref="ISlotService"/> на основе <see cref="ApplicationDbContext"/>.
/// Шаг сетки слотов — 15 минут.
/// </summary>
public class SlotService : ISlotService
{
    private const int SlotStepMinutes = 15;

    private readonly ApplicationDbContext _db;

    public SlotService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TimeOnly>> GetAvailableSlotsAsync(
        int masterId,
        int branchId,
        int serviceId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceId == serviceId, cancellationToken);

        if (service is null)
        {
            return Array.Empty<TimeOnly>();
        }

        var workIntervals = await _db.WorkSchedules
            .AsNoTracking()
            .Where(w => w.MasterId == masterId
                     && w.BranchId == branchId
                     && w.WorkDate == date
                     && w.ScheduleType == ScheduleType.Work)
            .Select(w => new { w.StartTime, w.EndTime })
            .ToListAsync(cancellationToken);

        if (workIntervals.Count == 0)
        {
            return Array.Empty<TimeOnly>();
        }

        var busyIntervals = await GetBusyIntervalsAsync(masterId, date, cancellationToken);

        var slots = new List<TimeOnly>();
        var duration = TimeSpan.FromMinutes(service.DurationMinutes);

        foreach (var work in workIntervals)
        {
            var candidate = work.StartTime;

            while (candidate.Add(duration) <= work.EndTime)
            {
                var candidateEnd = candidate.Add(duration);

                if (!OverlapsAny(candidate, candidateEnd, busyIntervals))
                {
                    slots.Add(candidate);
                }

                candidate = candidate.AddMinutes(SlotStepMinutes);
            }
        }

        return slots
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }

    private async Task<List<(TimeOnly Start, TimeOnly End)>> GetBusyIntervalsAsync(
        int masterId,
        DateOnly date,
        CancellationToken ct)
    {
        var nonWorkSchedule = await _db.WorkSchedules
            .AsNoTracking()
            .Where(w => w.MasterId == masterId
                     && w.WorkDate == date
                     && w.ScheduleType != ScheduleType.Work)
            .Select(w => new { w.StartTime, w.EndTime })
            .ToListAsync(ct);

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = date.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var bookings = await _db.Bookings
            .AsNoTracking()
            .Where(b => b.MasterId == masterId
                     && b.Status != BookingStatus.Cancelled
                     && b.StartDateTime >= dayStart
                     && b.StartDateTime < dayEnd)
            .Select(b => new { b.StartDateTime, b.DurationMinutes })
            .ToListAsync(ct);

        var busy = new List<(TimeOnly Start, TimeOnly End)>(nonWorkSchedule.Count + bookings.Count);

        foreach (var s in nonWorkSchedule)
        {
            busy.Add((s.StartTime, s.EndTime));
        }

        foreach (var b in bookings)
        {
            var start = TimeOnly.FromDateTime(b.StartDateTime);
            var end = start.AddMinutes(b.DurationMinutes);
            busy.Add((start, end));
        }

        return busy;
    }

    private static bool OverlapsAny(
        TimeOnly start,
        TimeOnly end,
        List<(TimeOnly Start, TimeOnly End)> busy)
    {
        foreach (var b in busy)
        {
            if (start < b.End && b.Start < end)
            {
                return true;
            }
        }
        return false;
    }
}
