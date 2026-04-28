using Microsoft.AspNetCore.Identity.UI.Services;

namespace BarbershopCrm.Web.Common;

/// <summary>
/// Реализация <see cref="IEmailSender"/> для разработки: вместо реальной
/// отправки пишет письмо в лог. Используется страницами «Восстановление
/// пароля» и «Подтверждение e-mail», чтобы они не падали при отсутствии
/// SMTP-провайдера.
/// </summary>
public sealed class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger) => _logger = logger;

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation(
            "[DEV-EMAIL] Адресат: {Email}; Тема: {Subject}; Содержимое: {Body}",
            email, subject, htmlMessage);
        return Task.CompletedTask;
    }
}
