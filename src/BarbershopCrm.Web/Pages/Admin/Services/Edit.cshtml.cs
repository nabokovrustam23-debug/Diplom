using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BarbershopCrm.Web.Pages.Admin.Services;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public CreateModel.ServiceInput Input { get; set; } = new();
    public int ServiceId { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var s = await _db.Services.FindAsync(id);
        if (s is null) return RedirectToPage("Index");
        ServiceId = s.ServiceId;
        Input = new()
        {
            Name = s.Name,
            Description = s.Description,
            DurationMinutes = s.DurationMinutes,
            Price = s.Price,
            DisplayOrder = s.DisplayOrder,
            Category = s.Category,
            IsActive = s.IsActive
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        ServiceId = id;
        if (!ModelState.IsValid) return Page();
        if (Input.DurationMinutes % 15 != 0)
        {
            ModelState.AddModelError(nameof(Input.DurationMinutes), "Кратно 15 минутам");
            return Page();
        }
        var s = await _db.Services.FindAsync(id);
        if (s is null) return RedirectToPage("Index");
        s.Name = Input.Name.Trim();
        s.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();
        s.DurationMinutes = Input.DurationMinutes;
        s.Price = Input.Price;
        s.DisplayOrder = Input.DisplayOrder;
        s.Category = Input.Category;
        s.IsActive = Input.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
