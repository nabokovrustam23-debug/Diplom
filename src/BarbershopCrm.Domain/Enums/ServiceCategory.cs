namespace BarbershopCrm.Domain.Enums;

/// <summary>
/// Категория услуги прайс-листа. Используется для быстрых фильтров
/// на публичной странице записи и для группировки в админ-каталоге.
/// </summary>
public enum ServiceCategory
{
    Other = 0,
    Haircut = 1,
    Beard = 2,
    Shave = 3,
    Kids = 4,
    Coloring = 5
}
