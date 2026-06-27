using CodexResetTray.Core.Protocol;
using CodexResetTray.Core.Security;

namespace CodexResetTray.Tests;

public sealed class ProtocolAndRedactionTests
{
    [Fact]
    public void CreateStartupMessages_only_requests_read_only_rate_limit_data()
    {
        var messages = AppServerProtocol.CreateStartupMessages("test_client", "Test Client", "0.0.0").ToArray();
        var serialized = string.Join('\n', messages);

        Assert.Contains("\"method\":\"initialize\"", serialized);
        Assert.Contains("\"method\":\"initialized\"", serialized);
        Assert.Contains("\"method\":\"account/rateLimits/read\"", serialized);
        Assert.DoesNotContain("consume", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reset", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("auth", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("logout", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecretRedactor_removes_common_token_shapes()
    {
        const string text = "Authorization: Bearer eyJabc.def.ghi and key sk-test1234567890abcdef plus path C:/Users/Alice/.codex/auth.json";

        var redacted = SecretRedactor.Redact(text);

        Assert.DoesNotContain("eyJabc.def.ghi", redacted);
        Assert.DoesNotContain("sk-test", redacted);
        Assert.DoesNotContain("Alice", redacted);
        Assert.Contains("<bearer-token>", redacted);
        Assert.Contains("<api-key>", redacted);
        Assert.Contains("<user-path>", redacted);
    }
}
