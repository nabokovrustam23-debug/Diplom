using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Infrastructure.Identity;

/// <summary>
/// Создаёт стартовые роли и демо-учётные записи для разграничения доступа:
/// владелец сети, администратор филиала и по одной учётной записи на каждого
/// мастера (привязанная к его Persona через PersonaId).
/// </summary>
public static class IdentitySeeder
{
    public const string OwnerRole = "Owner";
    public const string AdminRole = "Admin";
    public const string MasterRole = "Master";
    public const string ClientRole = "Client";

    public const string OwnerEmail = "owner@tihiychas.ru";
    public const string OwnerPassword = "Owner!2026";

    public const string AdminEmail = "admin@tihiychas.ru";
    public const string AdminPassword = "Admin!2026";

    /// <summary>Единый пароль для демо-учёток мастеров: «Master!2026».</summary>
    public const string MasterPassword = "Master!2026";

    public static async Task SeedAsync(
        ApplicationDbContext db,
        RoleManager<IdentityRole<int>> roleManager,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken = default)
    {
        foreach (var role in new[] { OwnerRole, AdminRole, MasterRole, ClientRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }

        await EnsureOwnerAsync(db, userManager, cancellationToken);
        await EnsureAdminAsync(db, userManager, cancellationToken);
        await EnsureMasterAccountsAsync(db, userManager, cancellationToken);
    }

    private static async Task EnsureOwnerAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> um, CancellationToken ct)
    {
        if (await um.FindByEmailAsync(OwnerEmail) is not null) return;

        var persona = await db.Personas.FirstOrDefaultAsync(p => p.Email == OwnerEmail, ct);
        if (persona is null)
        {
            persona = new Persona
            {
                LastName = "Владелец",
                FirstName = "Сети",
                Phone = "+79000000001",
                Email = OwnerEmail
            };
            db.Personas.Add(persona);
            await db.SaveChangesAsync(ct);
        }

        var user = new ApplicationUser
        {
            UserName = OwnerEmail,
            Email = OwnerEmail,
            EmailConfirmed = true,
            PersonaId = persona.PersonaId
        };
        var result = await um.CreateAsync(user, OwnerPassword);
        if (result.Succeeded)
        {
            await um.AddToRoleAsync(user, OwnerRole);
        }
    }

    private static async Task EnsureAdminAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> um, CancellationToken ct)
    {
        if (await um.FindByEmailAsync(AdminEmail) is not null) return;

        var persona = await db.Personas.FirstOrDefaultAsync(p => p.Email == AdminEmail, ct);
        if (persona is null)
        {
            persona = new Persona
            {
                LastName = "Администратор",
                FirstName = "Сети",
                Phone = "+79000000002",
                Email = AdminEmail
            };
            db.Personas.Add(persona);
            await db.SaveChangesAsync(ct);
        }

        var user = new ApplicationUser
        {
            UserName = AdminEmail,
            Email = AdminEmail,
            EmailConfirmed = true,
            PersonaId = persona.PersonaId
        };
        var result = await um.CreateAsync(user, AdminPassword);
        if (result.Succeeded)
        {
            await um.AddToRoleAsync(user, AdminRole);
        }
    }

    /// <summary>
    /// Создаёт для каждого уже сидированного мастера учётку вида
    /// master{MasterId}@tihiychas.ru с единым паролем. Эта учётка привязана
    /// к Persona мастера, что позволит кабинету мастера фильтровать свои
    /// записи и расписание по PersonaId → MasterId.
    /// </summary>
    private static async Task EnsureMasterAccountsAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> um, CancellationToken ct)
    {
        var masters = await db.Masters.Include(m => m.Persona).ToListAsync(ct);
        foreach (var master in masters)
        {
            var email = $"master{master.MasterId}@tihiychas.ru";
            if (await um.FindByEmailAsync(email) is not null) continue;

            // Если у Persona ещё не заполнен Email — проставим его, чтобы
            // интерфейс Identity мог показать учётку без None-значений.
            if (string.IsNullOrWhiteSpace(master.Persona.Email))
            {
                master.Persona.Email = email;
                await db.SaveChangesAsync(ct);
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                PhoneNumber = master.Persona.Phone,
                PersonaId = master.PersonaId
            };
            var result = await um.CreateAsync(user, MasterPassword);
            if (result.Succeeded)
            {
                await um.AddToRoleAsync(user, MasterRole);
            }
        }
    }
}
