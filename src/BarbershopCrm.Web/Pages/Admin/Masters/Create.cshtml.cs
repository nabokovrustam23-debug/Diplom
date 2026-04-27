using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Masters;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public MasterInput Input { get; set; } = new();
    public IList<SelectListItem> BranchOptions { get; private set; } = new List<SelectListItem>();
    public IList<SelectListItem> ServiceOptions { get; private set; } = new List<SelectListItem>();

    public class MasterInput
    {
        [Required, StringLength(80)] public string LastName { get; set; } = "";
        [Required, StringLength(80)] public string FirstName { get; set; } = "";
        [StringLength(80)] public string? MiddleName { get; set; }
        [Required, Phone, StringLength(32)] public string Phone { get; set; } = "";
        [EmailAddress, StringLength(120)] public string? Email { get; set; }
        public Gender? Gender { get; set; } = BarbershopCrm.Domain.Enums.Gender.Male;
        public DateOnly? BirthDate { get; set; }

        [Required, StringLength(80)] public string Position { get; set; } = "Барбер";
        public DateOnly HireDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        [StringLength(1000)] public string? Bio { get; set; }
        public bool IsActive { get; set; } = true;

        [Required(ErrorMessage = "Выберите филиал.")]
        public int BranchId { get; set; }
        public List<int> ServiceIds { get; set; } = new();
    }

    public async Task OnGetAsync() => await LoadOptionsAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await LoadOptionsAsync(); return Page(); }

        var phone = NormalizePhone(Input.Phone);
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.Phone == phone);
        if (persona is null)
        {
            persona = new Persona
            {
                LastName = Input.LastName.Trim(),
                FirstName = Input.FirstName.Trim(),
                MiddleName = string.IsNullOrWhiteSpace(Input.MiddleName) ? null : Input.MiddleName.Trim(),
                Phone = phone,
                Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim(),
                Gender = Input.Gender,
                BirthDate = Input.BirthDate
            };
            _db.Personas.Add(persona);
            await _db.SaveChangesAsync();
        }

        var master = new Master
        {
            PersonaId = persona.PersonaId,
            Position = Input.Position.Trim(),
            HireDate = Input.HireDate,
            Bio = string.IsNullOrWhiteSpace(Input.Bio) ? null : Input.Bio.Trim(),
            IsActive = Input.IsActive
        };
        _db.Masters.Add(master);
        await _db.SaveChangesAsync();

        // Один мастер — один филиал: добавляем единственную связь.
        _db.MasterBranches.Add(new MasterBranch { MasterId = master.MasterId, BranchId = Input.BranchId });
        foreach (var sid in Input.ServiceIds.Distinct())
            _db.MasterServices.Add(new MasterService { MasterId = master.MasterId, ServiceId = sid });
        await _db.SaveChangesAsync();

        return RedirectToPage("Index");
    }

    private async Task LoadOptionsAsync()
    {
        BranchOptions = await _db.Branches.AsNoTracking().OrderBy(b => b.Name)
            .Select(b => new SelectListItem(b.Name, b.BranchId.ToString())).ToListAsync();
        ServiceOptions = await _db.Services.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SelectListItem($"{s.Name} ({s.DurationMinutes} мин · {s.Price:0} ₽)", s.ServiceId.ToString())).ToListAsync();
    }

    public static string NormalizePhone(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("8") && digits.Length == 11) digits = "7" + digits[1..];
        return digits.Length == 0 ? raw : "+" + digits;
    }
}
