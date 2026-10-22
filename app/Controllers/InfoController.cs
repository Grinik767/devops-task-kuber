using Microsoft.AspNetCore.Mvc;

namespace HomeworkApp.Controllers;

[ApiController]
[Route("/")]
public class InfoController(IConfiguration config) : ControllerBase
{
    public const string StudentName = "Киселев Григорий Александрович";
    public const string BuildVersion = "v1";
    public const string FallbackGreeting = "Сообщения нет";
    public const string FallbackStage = "Окружения нет";

    [HttpGet]
    public IActionResult Get()
    {
        var greeting = config["APP_MESSAGE"] ?? FallbackGreeting;
        var stage = config["APP_ENVIRONMENT"] ?? FallbackStage;
        var hasToken = !string.IsNullOrEmpty(config["APP_TOKEN"]);

        return Ok(new
        {
            message = greeting,
            author = StudentName,
            environment = stage,
            tokenConfigured = hasToken,
            machineName = Environment.MachineName,
            appVersion = BuildVersion
        });
    }
}
