using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Services;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<Service> Services { get; private set; } = new List<Service>();

    public async Task OnGetAsync()
    {
        Services = await _db.Services.AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Переключает признак активности услуги. Отключённая услуга остаётся
    /// в базе (вся история записей сохраняется), но скрывается из публичного
    /// каталога и из шага выбора услуги.
    /// </summary>
    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var s = await _db.Services.FindAsync(id);
        if (s is not null)
        {
            s.IsActive = !s.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    /// <summary>
    /// Сдвигает услугу вверх в каталоге (меняет DisplayOrder местами
    /// с услугой, идущей выше в текущей сортировке).
    /// </summary>
    public async Task<IActionResult> OnPostMoveUpAsync(int id) => await MoveAsync(id, up: true);
    public async Task<IActionResult> OnPostMoveDownAsync(int id) => await MoveAsync(id, up: false);

    private async Task<IActionResult> MoveAsync(int id, bool up)
    {
        var ordered = await _db.Services
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
            .ToListAsync();
        var idx = ordered.FindIndex(s => s.ServiceId == id);
        if (idx < 0) return RedirectToPage();
        var targetIdx = up ? idx - 1 : idx + 1;
        if (targetIdx < 0 || targetIdx >= ordered.Count) return RedirectToPage();

        (ordered[idx].DisplayOrder, ordered[targetIdx].DisplayOrder) =
            (ordered[targetIdx].DisplayOrder, ordered[idx].DisplayOrder);
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }
}
