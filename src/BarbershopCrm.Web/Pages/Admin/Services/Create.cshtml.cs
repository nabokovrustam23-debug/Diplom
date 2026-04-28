using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BarbershopCrm.Web.Pages.Admin.Services;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public ServiceInput Input { get; set; } = new();

    public class ServiceInput
    {
        [Required, StringLength(120)]
        public string Name { get; set; } = "";
        [StringLength(500)]
        public string? Description { get; set; }

        [Range(15, 480, ErrorMessage = "Длительность 15–480 минут")]
        public int DurationMinutes { get; set; } = 60;

        [Range(0, 1_000_000)]
        public decimal Price { get; set; } = 1000;

        [Range(0, 9999)]
        [Display(Name = "Порядок вывода")]
        public int DisplayOrder { get; set; }

        [Display(Name = "Категория")]
        public ServiceCategory Category { get; set; } = ServiceCategory.Haircut;

        [Display(Name = "Активна")]
        public bool IsActive { get; set; } = true;
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (Input.DurationMinutes % 15 != 0)
        {
            ModelState.AddModelError(nameof(Input.DurationMinutes), "Кратно 15 минутам");
            return Page();
        }

        _db.Services.Add(new Service
        {
            Name = Input.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
            DurationMinutes = Input.DurationMinutes,
            Price = Input.Price,
            DisplayOrder = Input.DisplayOrder,
            Category = Input.Category,
            IsActive = Input.IsActive
        });
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
