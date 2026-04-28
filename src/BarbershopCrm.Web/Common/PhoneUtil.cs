using System.Text.RegularExpressions;

namespace BarbershopCrm.Web.Common;

/// <summary>
/// Единый валидатор и нормализатор телефонов. Принимает любые форматы
/// с цифрами, пробелами, скобками и плюсом. Канонизирует к виду
/// «+7XXXXXXXXXX» (10 цифр) или «+XXXXXXXXX…» при нероссийском номере.
/// Используется в формах регистрации/бронирования/приглашения сотрудников.
/// </summary>
public static class PhoneUtil
{
    private static readonly Regex DigitsOnly = new("\\D", RegexOptions.Compiled);

    /// <summary>Возвращает только цифры из произвольной строки.</summary>
    public static string Digits(string? input) =>
        string.IsNullOrEmpty(input) ? string.Empty : DigitsOnly.Replace(input, string.Empty);

    /// <summary>Канонизирует номер: 10 цифр → «+7XXXXXXXXXX»; 11 цифр с 8 → «+7…»;
    /// 11 цифр с 7 → «+7…»; иначе «+» + цифры. Пустую строку возвращает как пустую.</summary>
    public static string Normalize(string? input)
    {
        var d = Digits(input);
        if (d.Length == 0) return string.Empty;
        if (d.Length == 10) return "+7" + d;
        if (d.Length == 11 && (d[0] == '7' || d[0] == '8')) return "+7" + d[1..];
        return "+" + d;
    }

    /// <summary>Простой валидатор: длина после нормализации — 11–15 цифр.</summary>
    public static bool IsValid(string? input)
    {
        var d = Digits(input);
        return d.Length is >= 10 and <= 15;
    }
}
