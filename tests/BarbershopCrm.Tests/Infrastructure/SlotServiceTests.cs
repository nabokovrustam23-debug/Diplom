using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using ServiceEntity = BarbershopCrm.Domain.Entities.Service;
using BookingEntity = BarbershopCrm.Domain.Entities.Booking;

namespace BarbershopCrm.Tests.Infrastructure;

public class SlotServiceTests
{
    private static ApplicationDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(int branchId, int serviceId, int masterId, DateOnly date)>
        SeedAsync(ApplicationDbContext db, int durationMinutes = 60)
    {
        var branch = new Branch
        {
            Name = "Тестовый",
            Address = "Тестовая, 1",
            OpeningTime = new TimeOnly(10, 0),
            ClosingTime = new TimeOnly(20, 0)
        };
        var service = new ServiceEntity
        {
            Name = "Стрижка",
            DurationMinutes = durationMinutes,
            Price = 1000m
        };
        var persona = new Persona { LastName = "Test", FirstName = "Master", Phone = "+70000000000" };
        db.Branches.Add(branch);
        db.Services.Add(service);
        db.Personas.Add(persona);
        await db.SaveChangesAsync();

        var master = new Master
        {
            PersonaId = persona.PersonaId,
            Position = "Барбер",
            HireDate = new DateOnly(2024, 1, 1),
            IsActive = true
        };
        db.Masters.Add(master);
        await db.SaveChangesAsync();

        var date = new DateOnly(2026, 5, 1);

        // Утро 10:00–14:00, обед 14:00–15:00, вечер 15:00–18:00.
        db.WorkSchedules.Add(new WorkSchedule
        {
            MasterId = master.MasterId,
            BranchId = branch.BranchId,
            WorkDate = date,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(14, 0),
            ScheduleType = ScheduleType.Work
        });
        db.WorkSchedules.Add(new WorkSchedule
        {
            MasterId = master.MasterId,
            BranchId = branch.BranchId,
            WorkDate = date,
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(15, 0),
            ScheduleType = ScheduleType.Lunch
        });
        db.WorkSchedules.Add(new WorkSchedule
        {
            MasterId = master.MasterId,
            BranchId = branch.BranchId,
            WorkDate = date,
            StartTime = new TimeOnly(15, 0),
            EndTime = new TimeOnly(18, 0),
            ScheduleType = ScheduleType.Work
        });
        await db.SaveChangesAsync();

        return (branch.BranchId, service.ServiceId, master.MasterId, date);
    }

    [Fact]
    public async Task NoBookings_ReturnsAllSlotsThatFitInWorkInterval()
    {
        await using var db = NewDb();
        var (branchId, serviceId, masterId, date) = await SeedAsync(db, durationMinutes: 60);
        var sut = new SlotService(db);

        var slots = await sut.GetAvailableSlotsAsync(masterId, branchId, serviceId, date);

        // Утро 10:00–14:00 с услугой 60 мин и шагом 15 мин: 10:00, 10:15, ..., 13:00 → 13 слотов.
        // Вечер 15:00–18:00: 15:00, ..., 17:00 → 9 слотов. Итого 22.
        Assert.Equal(22, slots.Count);
        Assert.Equal(new TimeOnly(10, 0), slots[0]);
        Assert.Equal(new TimeOnly(17, 0), slots[^1]);
        Assert.DoesNotContain(new TimeOnly(13, 15), slots); // не помещается в 14:00.
    }

    [Fact]
    public async Task ExistingBooking_BlocksOverlappingSlots()
    {
        await using var db = NewDb();
        var (branchId, serviceId, masterId, date) = await SeedAsync(db, durationMinutes: 60);
        db.Bookings.Add(new BookingEntity
        {
            ClientId = 0,
            MasterId = masterId,
            ServiceId = serviceId,
            BranchId = branchId,
            StartDateTime = date.ToDateTime(new TimeOnly(11, 0)),
            DurationMinutes = 60,
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new SlotService(db);
        var slots = await sut.GetAvailableSlotsAsync(masterId, branchId, serviceId, date);

        // Слоты, перекрывающие 11:00–12:00, должны быть исключены: 10:15, 10:30, 10:45, 11:00, 11:15, 11:30, 11:45.
        Assert.DoesNotContain(new TimeOnly(10, 15), slots);
        Assert.DoesNotContain(new TimeOnly(11, 0), slots);
        Assert.DoesNotContain(new TimeOnly(11, 45), slots);
        Assert.Contains(new TimeOnly(10, 0), slots);
        Assert.Contains(new TimeOnly(12, 0), slots);
    }

    [Fact]
    public async Task CancelledBooking_DoesNotBlockSlots()
    {
        await using var db = NewDb();
        var (branchId, serviceId, masterId, date) = await SeedAsync(db, durationMinutes: 60);
        db.Bookings.Add(new BookingEntity
        {
            ClientId = 0,
            MasterId = masterId,
            ServiceId = serviceId,
            BranchId = branchId,
            StartDateTime = date.ToDateTime(new TimeOnly(11, 0)),
            DurationMinutes = 60,
            Status = BookingStatus.Cancelled,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new SlotService(db);
        var slots = await sut.GetAvailableSlotsAsync(masterId, branchId, serviceId, date);

        Assert.Contains(new TimeOnly(11, 0), slots);
    }

    [Fact]
    public async Task LunchInterval_BlocksOverlappingSlots()
    {
        await using var db = NewDb();
        var (branchId, serviceId, masterId, date) = await SeedAsync(db, durationMinutes: 60);
        var sut = new SlotService(db);

        var slots = await sut.GetAvailableSlotsAsync(masterId, branchId, serviceId, date);

        // Обед 14:00–15:00. Слот 13:15 с длительностью 60 мин уезжает в обед — должен быть отброшен.
        // (Само ограничение «слот должен помещаться в Work-интервал 10:00–14:00» уже исключает 13:15,
        // но проверим явно, что 13:15 нет в результате.)
        Assert.DoesNotContain(new TimeOnly(13, 15), slots);
    }

    [Fact]
    public async Task NoWorkSchedule_ReturnsEmpty()
    {
        await using var db = NewDb();
        var (branchId, serviceId, masterId, date) = await SeedAsync(db, durationMinutes: 60);
        var sut = new SlotService(db);

        var slots = await sut.GetAvailableSlotsAsync(masterId, branchId, serviceId, date.AddDays(7));

        Assert.Empty(slots);
    }

    [Fact]
    public async Task UnknownService_ReturnsEmpty()
    {
        await using var db = NewDb();
        var (branchId, _, masterId, date) = await SeedAsync(db);
        var sut = new SlotService(db);

        var slots = await sut.GetAvailableSlotsAsync(masterId, branchId, serviceId: 99999, date);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task ShortService_HasMoreSlots()
    {
        await using var db = NewDb();
        var (branchId, serviceId, masterId, date) = await SeedAsync(db, durationMinutes: 30);
        var sut = new SlotService(db);

        var slots = await sut.GetAvailableSlotsAsync(masterId, branchId, serviceId, date);

        // Утро 10:00–14:00, услуга 30 мин, шаг 15 мин: 10:00 ... 13:30 → 15 слотов.
        // Вечер 15:00–18:00: 15:00 ... 17:30 → 11 слотов. Итого 26.
        Assert.Equal(26, slots.Count);
    }

    [Fact]
    public async Task BufferBetweenBookings_BlocksAdjacentSlots()
    {
        await using var db = NewDb();
        var (branchId, serviceId, masterId, date) = await SeedAsync(db, durationMinutes: 60);
        // Существующая запись 11:00–12:00.
        db.Bookings.Add(new BookingEntity
        {
            ClientId = 0,
            MasterId = masterId,
            ServiceId = serviceId,
            BranchId = branchId,
            StartDateTime = date.ToDateTime(new TimeOnly(11, 0)),
            DurationMinutes = 60,
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Буфер 15 минут: занятость расширяется до 10:45–12:15.
        var sut = new SlotService(db, bufferMinutes: 15);
        var slots = await sut.GetAvailableSlotsAsync(masterId, branchId, serviceId, date);

        // 12:00 должен быть исключён (новая запись 12:00–13:00 пересечётся с буфером 12:00–12:15).
        Assert.DoesNotContain(new TimeOnly(12, 0), slots);
        // 12:15 — допустим (12:15–13:15 не пересекает буфер).
        Assert.Contains(new TimeOnly(12, 15), slots);
        // 10:00 — без буфера допустим, но услуга 60 мин закончится в 11:00 и пересечёт
        // буфер 10:45–11:00 → должно быть исключено.
        Assert.DoesNotContain(new TimeOnly(10, 0), slots);
    }
}
