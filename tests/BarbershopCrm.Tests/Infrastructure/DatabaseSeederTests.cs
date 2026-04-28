using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Tests.Infrastructure;

public class DatabaseSeederTests
{
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SeedAsync_FillsAllReferenceTables()
    {
        await using var db = CreateInMemoryContext();

        await DatabaseSeeder.SeedAsync(db);

        Assert.Equal(3, await db.Branches.CountAsync());
        Assert.Equal(6, await db.Services.CountAsync());
        Assert.Equal(5, await db.Masters.CountAsync());
        Assert.Equal(5, await db.Personas.CountAsync());

        Assert.True(await db.MasterBranches.AnyAsync());
        Assert.True(await db.MasterServices.AnyAsync());

        // 7 дней × 5 мастеров × 3 интервала (утро/обед/вечер) = 105 записей.
        Assert.Equal(105, await db.WorkSchedules.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        await using var db = CreateInMemoryContext();

        await DatabaseSeeder.SeedAsync(db);
        var firstCount = await db.Branches.CountAsync();

        await DatabaseSeeder.SeedAsync(db);
        var secondCount = await db.Branches.CountAsync();

        Assert.Equal(firstCount, secondCount);
    }

    [Fact]
    public async Task SeedAsync_AllMastersHavePersona()
    {
        await using var db = CreateInMemoryContext();

        await DatabaseSeeder.SeedAsync(db);

        var masters = await db.Masters.Include(m => m.Persona).ToListAsync();
        Assert.All(masters, m => Assert.NotNull(m.Persona));
        Assert.All(masters, m => Assert.False(string.IsNullOrWhiteSpace(m.Persona.LastName)));
    }

    [Fact]
    public async Task SeedAsync_LunchSchedulesAreOneHour()
    {
        await using var db = CreateInMemoryContext();

        await DatabaseSeeder.SeedAsync(db);

        var lunches = await db.WorkSchedules
            .Where(w => w.ScheduleType == ScheduleType.Lunch)
            .ToListAsync();

        Assert.NotEmpty(lunches);
        Assert.All(lunches, l => Assert.Equal(60, (int)(l.EndTime - l.StartTime).TotalMinutes));
    }
}
