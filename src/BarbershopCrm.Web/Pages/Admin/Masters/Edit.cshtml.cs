using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Masters;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public CreateModel.MasterInput Input { get; set; } = new();
    public int MasterId { get; private set; }
    public IList<SelectListItem> BranchOptions { get; private set; } = new List<SelectListItem>();
    public IList<SelectListItem> ServiceOptions { get; private set; } = new List<SelectListItem>();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var m = await _db.Masters
            .Include(x => x.Persona)
            .Include(x => x.MasterBranches)
            .Include(x => x.MasterServices)
            .FirstOrDefaultAsync(x => x.MasterId == id);
        if (m is null) return RedirectToPage("Index");

        MasterId = id;
        Input = new()
        {
            LastName = m.Persona.LastName,
            FirstName = m.Persona.FirstName,
            MiddleName = m.Persona.MiddleName,
            Phone = m.Persona.Phone,
            Email = m.Persona.Email,
            Gender = m.Persona.Gender,
            BirthDate = m.Persona.BirthDate,
            Position = m.Position,
            HireDate = m.HireDate,
            Bio = m.Bio,
            IsActive = m.IsActive,
            BranchId = m.MasterBranches.Select(mb => mb.BranchId).FirstOrDefault(),
            ServiceIds = m.MasterServices.Select(ms => ms.ServiceId).ToList()
        };
        await LoadOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        MasterId = id;
        if (!ModelState.IsValid) { await LoadOptionsAsync(); return Page(); }

        var m = await _db.Masters
            .Include(x => x.Persona)
            .Include(x => x.MasterBranches)
            .Include(x => x.MasterServices)
            .FirstOrDefaultAsync(x => x.MasterId == id);
        if (m is null) return RedirectToPage("Index");

        m.Persona.LastName = Input.LastName.Trim();
        m.Persona.FirstName = Input.FirstName.Trim();
        m.Persona.MiddleName = string.IsNullOrWhiteSpace(Input.MiddleName) ? null : Input.MiddleName.Trim();
        m.Persona.Phone = CreateModel.NormalizePhone(Input.Phone);
        m.Persona.Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim();
        m.Persona.Gender = Input.Gender;
        m.Persona.BirthDate = Input.BirthDate;

        m.Position = Input.Position.Trim();
        m.HireDate = Input.HireDate;
        m.Bio = string.IsNullOrWhiteSpace(Input.Bio) ? null : Input.Bio.Trim();
        m.IsActive = Input.IsActive;

        // Привязка «мастер — один филиал»: удаляем все старые связи и добавляем одну.
        foreach (var existing in m.MasterBranches.ToList())
            _db.MasterBranches.Remove(existing);
        if (Input.BranchId > 0)
            _db.MasterBranches.Add(new MasterBranch { MasterId = id, BranchId = Input.BranchId });

        var newServices = Input.ServiceIds.Distinct().ToHashSet();
        var oldServices = m.MasterServices.Select(ms => ms.ServiceId).ToHashSet();
        foreach (var add in newServices.Except(oldServices))
            _db.MasterServices.Add(new MasterService { MasterId = id, ServiceId = add });
        foreach (var rm in oldServices.Except(newServices))
        {
            var ent = m.MasterServices.First(ms => ms.ServiceId == rm);
            _db.MasterServices.Remove(ent);
        }

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
}
