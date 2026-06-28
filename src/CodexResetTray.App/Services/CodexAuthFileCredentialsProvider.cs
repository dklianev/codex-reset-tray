using System.IO;
using System.Text.Json;

namespace CodexResetTray.App.Services;

public sealed class CodexAuthFileCredentialsProvider : ICodexAuthCredentialsProvider
{
    private readonly string _authPath;

    public CodexAuthFileCredentialsProvider()
        : this(ResolveDefaultAuthPath())
    {
    }

    internal CodexAuthFileCredentialsProvider(string authPath)
    {
        _authPath = authPath;
    }

    public CodexAuthCredentials Read()
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_authPath));
            var root = document.RootElement;
            if (!TryGetProperty(root, "tokens", out var tokens)
                || tokens.ValueKind != JsonValueKind.Object
                || !TryReadString(tokens, "access_token", out var accessToken)
                || !TryReadString(tokens, "account_id", out var accountId))
            {
                throw new InvalidOperationException("Codex auth file is missing the credentials required for expiry lookup.");
            }

            return new CodexAuthCredentials(accessToken, accountId);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Codex auth file could not be parsed for expiry lookup.", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Codex auth file could not be read for expiry lookup.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException("Codex auth file could not be read for expiry lookup.", ex);
        }
    }

    private static string ResolveDefaultAuthPath()
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(codexHome))
        {
            return Path.Combine(codexHome, "auth.json");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "auth.json");
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!TryGetProperty(element, propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            return false;
        }

        value = property.GetString()!;
        return true;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }
}
