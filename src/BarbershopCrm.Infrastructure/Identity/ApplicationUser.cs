using BarbershopCrm.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace BarbershopCrm.Infrastructure.Identity;

/// <summary>
/// Учётная запись пользователя системы.
/// Расширяет стандартный <see cref="IdentityUser{TKey}"/> внешним ключом
/// на <see cref="Persona"/> — общую сущность с персональными данными.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = null!;
}
