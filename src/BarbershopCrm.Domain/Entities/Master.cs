namespace BarbershopCrm.Domain.Entities;

public class Master
{
    public int MasterId { get; set; }
    public int PersonaId { get; set; }
    public string Position { get; set; } = null!;
    public DateOnly HireDate { get; set; }
    public string? AvatarPath { get; set; }
    public string? Bio { get; set; }
    public bool IsActive { get; set; } = true;

    public Persona Persona { get; set; } = null!;
    public ICollection<MasterBranch> MasterBranches { get; set; } = new List<MasterBranch>();
    public ICollection<MasterService> MasterServices { get; set; } = new List<MasterService>();
    public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
