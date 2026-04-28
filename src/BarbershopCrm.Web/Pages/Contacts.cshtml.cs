using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages;

/// <summary>Публичная страница контактов: адреса, часы работы и e-mail админа.</summary>
public class ContactsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public ContactsModel(ApplicationDbContext db) => _db = db;

    public IList<Branch> Branches { get; private set; } = new List<Branch>();

    public async Task OnGetAsync()
    {
        Branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
    }
}
