using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BarbershopCrm.Web.Pages.Admin.Branches;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public CreateModel.BranchInput Input { get; set; } = new();
    public int BranchId { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var b = await _db.Branches.FindAsync(id);
        if (b is null) return RedirectToPage("Index");
        BranchId = b.BranchId;
        Input = new()
        {
            Name = b.Name, Address = b.Address, Phone = b.Phone,
            OpeningTime = b.OpeningTime, ClosingTime = b.ClosingTime
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        BranchId = id;
        if (!ModelState.IsValid) return Page();
        if (Input.ClosingTime <= Input.OpeningTime)
        {
            ModelState.AddModelError(nameof(Input.ClosingTime), "Время закрытия должно быть позже открытия");
            return Page();
        }

        var b = await _db.Branches.FindAsync(id);
        if (b is null) return RedirectToPage("Index");

        b.Name = Input.Name.Trim();
        b.Address = Input.Address.Trim();
        b.Phone = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim();
        b.OpeningTime = Input.OpeningTime;
        b.ClosingTime = Input.ClosingTime;
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
