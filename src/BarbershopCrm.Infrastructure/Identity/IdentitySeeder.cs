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

    public static async Task SeedAsync(
        ApplicationDbContext db,
        RoleManager<IdentityRole<int>> roleManager,
        UserManager<ApplicationUser> userManager,
        string? ownerEmail,
        string? ownerPassword,
        CancellationToken cancellationToken = default)
    {
        foreach (var role in new[] { OwnerRole, AdminRole, MasterRole, ClientRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }

        if (string.IsNullOrWhiteSpace(ownerEmail) || string.IsNullOrWhiteSpace(ownerPassword))
        {
            return;
        }

        ownerEmail = ownerEmail.Trim();

        if (await userManager.FindByEmailAsync(ownerEmail) is null)
        {
            var persona = await db.Personas
                .FirstOrDefaultAsync(p => p.Email == ownerEmail, cancellationToken);

            if (persona is null)
            {
                persona = new Persona
                {
                    LastName = "Владелец",
                    FirstName = "Сети",
                    Phone = "+79000000001",
                    Email = ownerEmail
                };
                db.Personas.Add(persona);
                await db.SaveChangesAsync(cancellationToken);
            }

            var user = new ApplicationUser
            {
                UserName = ownerEmail,
                Email = ownerEmail,
                EmailConfirmed = true,
                PersonaId = persona.PersonaId
            };

            var result = await userManager.CreateAsync(user, ownerPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, OwnerRole);
            }
        }
    }
}
