using BarbershopCrm.Domain.Enums;

namespace BarbershopCrm.Domain.Entities;

public class Persona
{
    public int PersonaId { get; set; }
    public string LastName { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string? MiddleName { get; set; }
    public string Phone { get; set; } = null!;
    public string? Email { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }

    public Master? Master { get; set; }
    public Client? Client { get; set; }
}
