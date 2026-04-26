namespace BarbershopCrm.Domain.Entities;

public class MasterBranch
{
    public int MasterId { get; set; }
    public int BranchId { get; set; }

    public Master Master { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
}
