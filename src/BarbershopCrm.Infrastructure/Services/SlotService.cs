using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Infrastructure.Services;

/// <summary>
/// Реализация <see cref="ISlotService"/> на основе <see cref="ApplicationDbContext"/>.
/// Шаг сетки слотов — 15 минут. Буфер между записями (на уборку/перерыв)
/// настраивается через <see cref="SlotServiceOptions"/>.
/// </summary>
public class SlotService : ISlotService
{
    private const int SlotStepMinutes = 15;

    private readonly ApplicationDbContext _db;
    private readonly int _bufferMinutes;

    public SlotService(ApplicationDbContext db) : this(db, bufferMinutes: 0) { }

    public SlotService(ApplicationDbContext db, int bufferMinutes)
    {
        _db = db;
        _bufferMinutes = Math.Max(0, bufferMinutes);
    }

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
            // Расширяем занятость на буфер с обеих сторон. Это гарантирует, что между
            // соседними записями (со стороны конца предыдущей и начала следующей)
            // будет промежуток не меньше bufferMinutes минут.
            var startWithBuffer = start.AddMinutes(-_bufferMinutes);
            var endWithBuffer = start.AddMinutes(b.DurationMinutes + _bufferMinutes);
            busy.Add((startWithBuffer, endWithBuffer));
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

    public async Task<IReadOnlyList<(TimeOnly Time, int MasterId)>> GetAvailableSlotsForAnyMasterAsync(
        int branchId,
        int serviceId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        // Берём всех мастеров филиала, которые умеют выполнять услугу.
        var masterIds = await _db.MasterBranches
            .AsNoTracking()
            .Where(mb => mb.BranchId == branchId
                && _db.MasterServices.Any(ms => ms.MasterId == mb.MasterId && ms.ServiceId == serviceId))
            .Where(mb => mb.Master.IsActive)
            .Select(mb => mb.MasterId)
            .ToListAsync(cancellationToken);

        var dict = new Dictionary<TimeOnly, int>();
        foreach (var masterId in masterIds.OrderBy(id => id))
        {
            var slots = await GetAvailableSlotsAsync(masterId, branchId, serviceId, date, cancellationToken);
            foreach (var t in slots)
            {
                if (!dict.ContainsKey(t))
                {
                    dict[t] = masterId;
                }
            }
        }

        return dict
            .OrderBy(kv => kv.Key)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }

    public async Task<(DateOnly Date, TimeOnly Time)?> GetNextAvailableSlotAsync(
        int masterId,
        int branchId,
        int serviceId,
        int horizonDays,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        for (var i = 0; i < horizonDays; i++)
        {
            var date = today.AddDays(i);
            var slots = await GetAvailableSlotsAsync(masterId, branchId, serviceId, date, cancellationToken);
            if (slots.Count > 0)
            {
                return (date, slots[0]);
            }
        }
        return null;
    }
}
