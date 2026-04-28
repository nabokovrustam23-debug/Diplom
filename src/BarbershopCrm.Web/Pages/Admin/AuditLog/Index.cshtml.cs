using BarbershopCrm.Infrastructure.Data;
using AuditLogEntity = BarbershopCrm.Domain.Entities.AuditLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.AuditLog;

/// <summary>
/// Журнал критичных действий сотрудников: создание пользователей, смена ролей,
/// сброс паролей, переносы записей. Доступ — только владельцу сети.
/// Размер страницы фиксирован 100 записей, новые сверху.
/// </summary>
[Authorize(Policy = "OwnerOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<AuditLogEntity> Items { get; private set; } = new List<AuditLogEntity>();

    [BindProperty(SupportsGet = true)] public string? Action { get; set; }
    [BindProperty(SupportsGet = true)] public string? EntityType { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

    public const int PageSize = 100;
    public int TotalRows { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalRows / PageSize));

    public async Task OnGetAsync()
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Action)) q = q.Where(a => a.Action == Action);
        if (!string.IsNullOrWhiteSpace(EntityType)) q = q.Where(a => a.EntityType == EntityType);
        TotalRows = await q.CountAsync();
        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages) PageNumber = TotalPages;
        Items = await q.OrderByDescending(a => a.AtUtc)
            .Skip((PageNumber - 1) * PageSize).Take(PageSize)
            .ToListAsync();
    }
}
