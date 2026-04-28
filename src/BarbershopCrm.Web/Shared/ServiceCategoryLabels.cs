using BarbershopCrm.Domain.Enums;

namespace BarbershopCrm.Web.Shared;

/// <summary>
/// Русскоязычные ярлыки категорий услуг — используются одинаково в публичных
/// фильтрах и в админ-форме редактирования.
/// </summary>
public static class ServiceCategoryLabels
{
    public static readonly IReadOnlyDictionary<ServiceCategory, string> Map =
        new Dictionary<ServiceCategory, string>
        {
            [ServiceCategory.Haircut] = "Стрижка",
            [ServiceCategory.Beard] = "Борода",
            [ServiceCategory.Shave] = "Бритьё",
            [ServiceCategory.Kids] = "Детям",
            [ServiceCategory.Coloring] = "Тонирование",
            [ServiceCategory.Other] = "Прочее"
        };

    public static string Label(ServiceCategory c) => Map.TryGetValue(c, out var s) ? s : c.ToString();

    /// <summary>
    /// Стабильный порядок категорий для меню и фильтров.
    /// </summary>
    public static readonly IReadOnlyList<ServiceCategory> Order = new[]
    {
        ServiceCategory.Haircut,
        ServiceCategory.Beard,
        ServiceCategory.Shave,
        ServiceCategory.Coloring,
        ServiceCategory.Kids,
        ServiceCategory.Other
    };
}
