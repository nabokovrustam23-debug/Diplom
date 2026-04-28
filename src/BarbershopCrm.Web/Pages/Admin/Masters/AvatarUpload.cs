using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BarbershopCrm.Web.Pages.Admin.Masters;

/// <summary>
/// Утилита для сохранения файла аватара мастера. Реализована «по-простому»,
/// без работы с изображениями (resize/EXIF-strip) — только проверка типа,
/// размера и фиксированное имя файла, чтобы избежать накопления старых аватаров
/// в папке uploads.
/// </summary>
internal static class AvatarUpload
{
    public const long MaxBytes = 2 * 1024 * 1024;
    public static readonly string[] AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

    /// <summary>Сохраняет файл, возвращает относительный путь (для рендера &lt;img&gt;)
    /// или null, если файл не был приложен либо отвалидировался.</summary>
    public static async Task<string?> SaveAsync(IWebHostEnvironment env, IFormFile? file, int masterId, ModelStateDictionary modelState)
    {
        if (file is null || file.Length == 0) return null;

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            modelState.AddModelError("Input.AvatarFile", "Поддерживаются только JPG, PNG и WEBP.");
            return null;
        }
        if (file.Length > MaxBytes)
        {
            modelState.AddModelError("Input.AvatarFile", "Файл больше 2 МБ. Сожмите изображение перед загрузкой.");
            return null;
        }

        var dir = Path.Combine(env.WebRootPath ?? "wwwroot", "uploads", "masters");
        Directory.CreateDirectory(dir);

        // Удаляем старые файлы с тем же masterId под любыми расширениями, чтобы
        // не оставлять «хвосты» при смене формата.
        foreach (var oldExt in AllowedExtensions)
        {
            var old = Path.Combine(dir, $"{masterId}{oldExt}");
            if (File.Exists(old)) File.Delete(old);
        }

        var fileName = $"{masterId}{ext}";
        var fullPath = Path.Combine(dir, fileName);
        await using (var fs = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(fs);
        }

        return $"/uploads/masters/{fileName}";
    }
}
