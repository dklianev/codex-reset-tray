using System.Text.RegularExpressions;

namespace CodexResetTray.Core.Security;

public static partial class SecretRedactor
{
    public static string Redact(string value)
    {
        var redacted = BearerRegex().Replace(value, "Bearer <bearer-token>");
        redacted = JwtRegex().Replace(redacted, "<jwt>");
        redacted = ApiKeyRegex().Replace(redacted, "<api-key>");
        redacted = UserPathRegex().Replace(redacted, "$1<user-path>$3");
        return redacted;
    }

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9_\-\.]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_\-]*\.[A-Za-z0-9_\-\.]+\b", RegexOptions.CultureInvariant)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"\bsk-[A-Za-z0-9_\-]{10,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyRegex();

    [GeneratedRegex(@"([A-Za-z]:[\\/]+Users[\\/]+)([^\\/]+)([\\/]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserPathRegex();
}
