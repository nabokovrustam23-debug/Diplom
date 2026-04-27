using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Infrastructure.Identity;

/// <summary>
/// Создаёт стартовые роли и учётную запись владельца сети.
/// </summary>
public static class IdentitySeeder
{
    public const string OwnerRole = "Owner";
    public const string AdminRole = "Admin";
    public const string MasterRole = "Master";
    public const string ClientRole = "Client";

    public const string OwnerEmail = "owner@tihiychas.ru";
    public const string OwnerPassword = "Owner!2026";

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

        if (await userManager.FindByEmailAsync(OwnerEmail) is null)
        {
            var persona = await db.Personas
                .FirstOrDefaultAsync(p => p.Email == OwnerEmail, cancellationToken);

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
                await db.SaveChangesAsync(cancellationToken);
            }

            var user = new ApplicationUser
            {
                UserName = OwnerEmail,
                Email = OwnerEmail,
                EmailConfirmed = true,
                PersonaId = persona.PersonaId
            };

            var result = await userManager.CreateAsync(user, OwnerPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, OwnerRole);
            }
        }
    }
}
