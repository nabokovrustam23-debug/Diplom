namespace BarbershopCrm.Domain.Entities;

public class Branch
{
    public int BranchId { get; set; }
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string? Phone { get; set; }
    public TimeOnly OpeningTime { get; set; }
    public TimeOnly ClosingTime { get; set; }

    public ICollection<MasterBranch> MasterBranches { get; set; } = new List<MasterBranch>();
    public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
