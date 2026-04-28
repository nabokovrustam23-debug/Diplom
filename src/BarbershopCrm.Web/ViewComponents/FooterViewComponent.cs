using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.ViewComponents;

/// <summary>
/// Футер сайта: список городов берётся из реальных филиалов
/// (первый сегмент адреса до запятой). Если филиалов нет — показываем
/// нейтральную строку, чтобы не было пустого блока.
/// </summary>
public class FooterViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    public FooterViewComponent(ApplicationDbContext db) => _db = db;

    public class FooterVm
    {
        public IReadOnlyList<string> Cities { get; init; } = Array.Empty<string>();
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var addresses = await _db.Branches.AsNoTracking()
            .Select(b => b.Address)
            .ToListAsync();
        var cities = addresses
            .Select(a => a?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
        return View(new FooterVm { Cities = cities });
    }
}
