using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BarbershopCrm.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public int Code { get; private set; } = 500;
    public string Title { get; private set; } = "Что-то пошло не так";
    public string Message { get; private set; } = "Мы уже знаем о проблеме и разбираемся. Попробуйте перейти на главную.";
    public string? RequestId { get; private set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    private readonly ILogger<ErrorModel> _logger;
    public ErrorModel(ILogger<ErrorModel> logger) => _logger = logger;

    public void OnGet(int? code)
    {
        Code = code ?? 500;
        (Title, Message) = Code switch
        {
            400 => ("Некорректный запрос", "Мы не смогли разобрать вашу форму. Попробуйте обновить страницу и заполнить заново."),
            401 => ("Нужна авторизация", "Для этого действия требуется войти в личный кабинет."),
            403 => ("Доступ запрещён", "У вашей роли нет прав на эту страницу."),
            404 => ("Страница не найдена", "Возможно, ссылка устарела или была введена неверно."),
            408 => ("Запрос занял слишком долго", "Попробуйте ещё раз — соединение оборвалось."),
            429 => ("Слишком много запросов", "Подождите минуту и попробуйте снова."),
            500 => ("Внутренняя ошибка сервера", "Мы уже знаем о проблеме и разбираемся. Попробуйте позже."),
            502 or 503 or 504 => ("Сервис временно недоступен", "Мы перезапускаем компоненты. Попробуйте через несколько минут."),
            _ => ($"Ошибка {Code}", "Что-то пошло не так. Попробуйте обновить страницу.")
        };
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        _logger.LogWarning("Error page rendered: status={Status} requestId={RequestId}", Code, RequestId);
    }
}
