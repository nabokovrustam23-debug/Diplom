namespace BarbershopCrm.Domain.Entities;

public class Client
{
    public int ClientId { get; set; }
    public int PersonaId { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public DateOnly? FirstVisitDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public Persona Persona { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
