using System.Text.Json;

namespace CodexResetTray.Core.Protocol;

public static class AppServerProtocol
{
    public const int InitializeRequestId = 1;
    public const int RateLimitsRequestId = 2;

    public static IEnumerable<string> CreateStartupMessages(string clientName, string title, string version)
    {
        yield return Serialize(new
        {
            method = "initialize",
            id = InitializeRequestId,
            @params = new
            {
                clientInfo = new
                {
                    name = clientName,
                    title,
                    version
                },
                capabilities = new
                {
                    experimentalApi = true,
                    requestAttestation = false,
                    optOutNotificationMethods = new[]
                    {
                        "thread/started",
                        "thread/status/changed",
                        "item/started",
                        "item/completed",
                        "turn/started",
                        "turn/completed"
                    }
                }
            }
        });

        yield return Serialize(new
        {
            method = "initialized",
            @params = new { }
        });

        yield return Serialize(new
        {
            method = "account/rateLimits/read",
            id = RateLimitsRequestId
        });
    }

    private static string Serialize(object message) =>
        JsonSerializer.Serialize(message, JsonSerializerOptions.Web);
}
