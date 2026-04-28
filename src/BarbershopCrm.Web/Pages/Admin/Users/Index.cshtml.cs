using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Users;

/// <summary>
/// Управление пользователями сети: просмотр списка учётных записей, их ролей
/// и связанной Persona. Позволяет назначать/снимать роли и менять основную
/// роль пользователя. Доступ — только владельцу сети (политика OwnerOnly).
/// </summary>
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public static readonly IReadOnlyList<string> AllRoles = new[]
    {
        IdentitySeeder.OwnerRole,
        IdentitySeeder.AdminRole,
        IdentitySeeder.MasterRole,
        IdentitySeeder.ClientRole
    };

    public IList<UserRow> Users { get; private set; } = new List<UserRow>();

    [BindProperty(SupportsGet = true)] public string? RoleFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    [TempData] public string? StatusMessage { get; set; }

    /// <summary>Сгенерированный временный пароль показываем один раз через TempData.</summary>
    [TempData] public string? ResetPasswordValue { get; set; }
    [TempData] public string? ResetPasswordEmail { get; set; }

    public record UserRow(
        int UserId,
        string Email,
        string FullName,
        string Phone,
        string RoleLabel,
        string RoleCode);

    public async Task OnGetAsync()
    {
        var users = await _db.Users
            .AsNoTracking()
            .Include(u => u.Persona)
            .OrderBy(u => u.Email)
            .ToListAsync();

        var rows = new List<UserRow>(users.Count);
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var role = roles.FirstOrDefault() ?? string.Empty;
            rows.Add(new UserRow(
                u.Id,
                u.Email ?? string.Empty,
                string.Join(' ', new[] { u.Persona?.LastName, u.Persona?.FirstName }.Where(s => !string.IsNullOrWhiteSpace(s))),
                u.Persona?.Phone ?? u.PhoneNumber ?? string.Empty,
                RoleLabel(role),
                role));
        }

        // Фильтры: по роли и простой полнотекстовый поиск по email/имени/телефону.
        IEnumerable<UserRow> filtered = rows;
        if (!string.IsNullOrWhiteSpace(RoleFilter) && AllRoles.Contains(RoleFilter))
        {
            filtered = filtered.Where(r => r.RoleCode == RoleFilter);
        }
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var needle = Q.Trim();
            filtered = filtered.Where(r =>
                r.Email.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || r.FullName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || r.Phone.Contains(needle));
        }

        Users = filtered.ToList();
    }

    public async Task<IActionResult> OnPostSetRoleAsync(int userId, string role)
    {
        if (!AllRoles.Contains(role))
        {
            StatusMessage = "Неизвестная роль.";
            return RedirectToPage();
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            StatusMessage = "Пользователь не найден.";
            return RedirectToPage();
        }

        // Защита от понижения последнего владельца сети: нельзя снять роль
        // Owner, если других владельцев в системе не осталось.
        if (User.Identity?.Name == user.Email && role != IdentitySeeder.OwnerRole)
        {
            var owners = await _userManager.GetUsersInRoleAsync(IdentitySeeder.OwnerRole);
            if (owners.Count <= 1)
            {
                StatusMessage = "Нельзя снять себя с последней роли владельца.";
                return RedirectToPage();
            }
        }

        var current = await _userManager.GetRolesAsync(user);
        if (current.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, current);
        }
        await _userManager.AddToRoleAsync(user, role);

        StatusMessage = $"Роль пользователя {user.Email} обновлена на «{RoleLabel(role)}».";
        return RedirectToPage();
    }

    /// <summary>Сбросить пароль пользователя на одноразовый сгенерированный.
    /// Реализуем через RemovePassword + AddPassword, чтобы не требовать токенов
    /// (мы не используем Email-провайдер). Новый пароль показываем в UI один раз.</summary>
    public async Task<IActionResult> OnPostResetPasswordAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            StatusMessage = "Пользователь не найден.";
            return RedirectToPage();
        }

        var newPassword = GenerateTemporaryPassword();
        var hadPassword = await _userManager.HasPasswordAsync(user);
        if (hadPassword)
        {
            var rm = await _userManager.RemovePasswordAsync(user);
            if (!rm.Succeeded)
            {
                StatusMessage = "Не удалось снять старый пароль: " + string.Join("; ", rm.Errors.Select(e => e.Description));
                return RedirectToPage();
            }
        }
        var add = await _userManager.AddPasswordAsync(user, newPassword);
        if (!add.Succeeded)
        {
            StatusMessage = "Не удалось установить новый пароль: " + string.Join("; ", add.Errors.Select(e => e.Description));
            return RedirectToPage();
        }

        ResetPasswordEmail = user.Email;
        ResetPasswordValue = newPassword;
        StatusMessage = $"Сгенерирован временный пароль для {user.Email}. Передайте его пользователю — после первого входа порекомендуйте сменить пароль.";
        return RedirectToPage();
    }

    /// <summary>Простой генератор пароля 12 символов: лат. буквы (без l/I/O/0) + цифры.
    /// Достаточно для одноразовой передачи через защищённый канал.</summary>
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[12];
        rng.GetBytes(bytes);
        var sb = new System.Text.StringBuilder(12);
        sb.Append(upper[bytes[0] % upper.Length]);
        sb.Append(lower[bytes[1] % lower.Length]);
        sb.Append(digits[bytes[2] % digits.Length]);
        sb.Append('!');
        var pool = upper + lower + digits;
        for (int i = 4; i < 12; i++) sb.Append(pool[bytes[i] % pool.Length]);
        return sb.ToString();
    }

    public static string RoleLabel(string roleCode) => roleCode switch
    {
        IdentitySeeder.OwnerRole => "Владелец",
        IdentitySeeder.AdminRole => "Администратор",
        IdentitySeeder.MasterRole => "Мастер",
        IdentitySeeder.ClientRole => "Клиент",
        _ => "—"
    };
}
