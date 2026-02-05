using Microsoft.Extensions.Logging;

namespace GovernmentCollections.API.Services;

public interface IRemitaLogService
{
    void LogInbound(string message);
    void LogOutbound(string message);
    void LogTransaction(string direction, string method, object data);
}

public class RemitaLogService : IRemitaLogService
{
    private readonly ILogger<RemitaLogService> _logger;

    public RemitaLogService(ILogger<RemitaLogService> logger)
    {
        _logger = logger;
    }

    public void LogInbound(string message)
    {
        _logger.LogInformation("[REMITA-INBOUND] {Message}", message);
        Console.WriteLine($"[REMITA-INBOUND] {message}");
    }

    public void LogOutbound(string message)
    {
        _logger.LogInformation("[REMITA-OUTBOUND] {Message}", message);
        Console.WriteLine($"[REMITA-OUTBOUND] {message}");
    }

    public void LogTransaction(string direction, string method, object data)
    {
        var message = $"{direction} - {method}: {System.Text.Json.JsonSerializer.Serialize(data)}";
        _logger.LogInformation("[REMITA-TRANSACTION] {Message}", message);
        Console.WriteLine($"[REMITA-TRANSACTION] {message}");
    }
}