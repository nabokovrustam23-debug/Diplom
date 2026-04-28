using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Booking;

public class ServiceModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public ServiceModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    /// <summary>
    /// Фильтр по категории. Пусто — показываются все активные услуги филиала.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ServiceCategory? Category { get; set; }

    /// <summary>
    /// Быстрый фильтр по длительности: short (&lt;=30), mid (45–60), long (&gt;=75).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? DurationFilter { get; set; }

    public Branch? Branch { get; private set; }

    public IReadOnlyList<Domain.Entities.Service> Services { get; private set; }
        = Array.Empty<Domain.Entities.Service>();

    /// <summary>
    /// Категории, которые действительно доступны в этом филиале — по ним
    /// строится меню быстрых фильтров (скрываем пустые).
    /// </summary>
    public IReadOnlyList<ServiceCategory> AvailableCategories { get; private set; }
        = Array.Empty<ServiceCategory>();

    public async Task<IActionResult> OnGetAsync()
    {
        Branch = await _db.Branches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BranchId == BranchId);

        if (Branch is null)
        {
            return RedirectToPage("Index");
        }

        // Базовый запрос: активные услуги, которые умеет хотя бы один мастер филиала.
        var baseQuery =
            from s in _db.Services
            where s.IsActive
               && _db.MasterServices.Any(ms =>
                    ms.ServiceId == s.ServiceId
                    && _db.MasterBranches.Any(mb =>
                        mb.MasterId == ms.MasterId && mb.BranchId == BranchId))
            select s;

        AvailableCategories = await baseQuery
            .Select(s => s.Category)
            .Distinct()
            .ToListAsync();

        var filtered = baseQuery;

        if (Category.HasValue)
        {
            filtered = filtered.Where(s => s.Category == Category.Value);
        }

        filtered = DurationFilter switch
        {
            "short" => filtered.Where(s => s.DurationMinutes <= 30),
            "mid" => filtered.Where(s => s.DurationMinutes > 30 && s.DurationMinutes <= 60),
            "long" => filtered.Where(s => s.DurationMinutes > 60),
            _ => filtered
        };

        Services = await filtered
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync();

        return Page();
    }
}
