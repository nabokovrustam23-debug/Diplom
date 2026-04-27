using BarbershopCrm.Domain.Enums;

namespace BarbershopCrm.Domain.Entities;

public class Booking
{
    public int BookingId { get; set; }
    public int ClientId { get; set; }
    public int MasterId { get; set; }
    public int ServiceId { get; set; }
    public int BranchId { get; set; }
    public DateTime StartDateTime { get; set; }
    public int DurationMinutes { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Created;
    public DateTime CreatedAt { get; set; }
    public DateTime? RescheduledFromUtc { get; set; }
    public string? Notes { get; set; }
    public string? Wishes { get; set; }

    public Client Client { get; set; } = null!;
    public Master Master { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Visit? Visit { get; set; }
}
