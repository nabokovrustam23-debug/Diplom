using System.Security.Claims;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Web.Common;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Tests.Infrastructure;

public class AuditLogTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ClaimsPrincipal MakeUser(string email)
    {
        var id = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, email),
        }, "Test");
        return new ClaimsPrincipal(id);
    }

    [Fact]
    public async Task Log_PersistsRecord_WithActorEmailAndDetails()
    {
        await using var db = CreateDb();
        var user = MakeUser("admin@x.ru");
        AuditLogger.Log(db, user, "Reschedule", "Booking", "42", "from=A to=B");
        await db.SaveChangesAsync();

        var record = await db.AuditLogs.SingleAsync();
        Assert.Equal("admin@x.ru", record.ActorEmail);
        Assert.Equal("Reschedule", record.Action);
        Assert.Equal("Booking", record.EntityType);
        Assert.Equal("42", record.EntityId);
        Assert.Equal("from=A to=B", record.Details);
        Assert.True((DateTime.UtcNow - record.AtUtc).TotalSeconds < 10);
    }

    [Fact]
    public async Task Log_NullActor_FallsBackToSystem()
    {
        await using var db = CreateDb();
        AuditLogger.Log(db, null, "Test", "Anything", "0");
        await db.SaveChangesAsync();
        var record = await db.AuditLogs.SingleAsync();
        Assert.Equal("system", record.ActorEmail);
    }
}
