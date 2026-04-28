using System.Security.Cryptography;
using System.Text;

namespace BarbershopCrm.Web.Common;

/// <summary>
/// Простая капча без внешних сервисов:
/// 1) honeypot-поле (<see cref="HoneypotField"/>) — невидимо в браузере, но боты его заполняют;
/// 2) арифметическая задача «A + B = ?», ответ проверяется по подписанному токену.
/// Хватает, чтобы отсечь массовый спам-бот, и не требует внешнего сервиса.
/// </summary>
public static class SimpleCaptcha
{
    public const string HoneypotField = "fax_number";
    public const string AnswerField = "captcha_answer";
    public const string TokenField = "captcha_token";

    public static (int A, int B, string Token, string Question) New()
    {
        var rng = RandomNumberGenerator.Create();
        var bytes = new byte[2];
        rng.GetBytes(bytes);
        int a = (bytes[0] % 9) + 1; // 1..9
        int b = (bytes[1] % 9) + 1; // 1..9
        var sum = a + b;
        return (a, b, Sign(sum), $"Сколько будет {a} + {b}?");
    }

    public static bool Verify(string? answer, string? token)
    {
        if (string.IsNullOrWhiteSpace(answer) || string.IsNullOrWhiteSpace(token)) return false;
        if (!int.TryParse(answer, out var n)) return false;
        return Sign(n) == token;
    }

    /// <summary>«Подпись» суммы: salt + sha256, чтобы клиент не подделал токен.
    /// Не криптостойкая защита и не нужна — это всего лишь капча.</summary>
    private static string Sign(int sum)
    {
        const string salt = "barbershop-captcha-v1";
        var bytes = Encoding.UTF8.GetBytes($"{salt}:{sum}");
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).Substring(0, 16);
    }

    /// <summary>Honeypot-поле должно остаться пустым; если заполнено — это бот.</summary>
    public static bool HoneypotIsClean(string? value) => string.IsNullOrEmpty(value);
}
