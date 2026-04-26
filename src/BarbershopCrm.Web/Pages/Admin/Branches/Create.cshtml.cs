using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BarbershopCrm.Web.Pages.Admin.Branches;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public BranchInput Input { get; set; } = new();

    public class BranchInput
    {
        [Required(ErrorMessage = "Укажите название")]
        [StringLength(100)]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Укажите адрес")]
        [StringLength(255)]
        public string Address { get; set; } = "";

        [Phone, StringLength(32)]
        public string? Phone { get; set; }

        [Required] public TimeOnly OpeningTime { get; set; } = new(10, 0);
        [Required] public TimeOnly ClosingTime { get; set; } = new(22, 0);
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (Input.ClosingTime <= Input.OpeningTime)
        {
            ModelState.AddModelError(nameof(Input.ClosingTime), "Время закрытия должно быть позже открытия");
            return Page();
        }

        _db.Branches.Add(new Branch
        {
            Name = Input.Name.Trim(),
            Address = Input.Address.Trim(),
            Phone = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim(),
            OpeningTime = Input.OpeningTime,
            ClosingTime = Input.ClosingTime
        });
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
