using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public IReadOnlyList<Branch> Branches { get; private set; } = Array.Empty<Branch>();
    public IReadOnlyList<string> Cities { get; private set; } = Array.Empty<string>();

    /// <summary>Фильтр по городу — берётся из первого сегмента адреса
    /// филиала. Пустое значение или «all» — показывать все филиалы.</summary>
    [BindProperty(SupportsGet = true)]
    public string? City { get; set; }

    public async Task OnGetAsync()
    {
        var all = await _db.Branches
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .ToListAsync();

        Cities = all
            .Select(b => ExtractCity(b.Address))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        if (!string.IsNullOrWhiteSpace(City) && !string.Equals(City, "all", StringComparison.OrdinalIgnoreCase))
        {
            Branches = all
                .Where(b => string.Equals(ExtractCity(b.Address), City, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            Branches = all;
        }
    }

    private static string? ExtractCity(string? address) =>
        address?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
}
