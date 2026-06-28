using System.Globalization;
using System.Text.Json;
using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.Core.Protocol;

public static class ResetCreditExpiryParser
{
    public static ResetCreditReport Parse(string json, DateTimeOffset fetchedAt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var availableCount = TryGetProperty(root, "available_count", out var availableCountElement)
            ? ReadNullableInt64(availableCountElement)
            : null;
        var credits = new List<ResetCreditInfo>();

        if (TryGetProperty(root, "credits", out var creditsElement)
            && creditsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in creditsElement.EnumerateArray())
            {
                var credit = TryReadCredit(element);
                if (credit is not null)
                {
                    credits.Add(credit);
                }
            }
        }

        return new ResetCreditReport(
            availableCount,
            credits.OrderBy(credit => credit.ExpiresAt).ToArray(),
            fetchedAt);
    }

    private static ResetCreditInfo? TryReadCredit(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var status = ReadString(element, "status");
        if (!string.Equals(status, "available", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var resetType = ReadString(element, "reset_type");
        if (string.IsNullOrWhiteSpace(resetType)
            || !TryReadDateTimeOffset(element, "granted_at", out var grantedAt)
            || !TryReadDateTimeOffset(element, "expires_at", out var expiresAt))
        {
            return null;
        }

        return new ResetCreditInfo(
            ReadString(element, "title") ?? "Reset credit",
            status!,
            resetType,
            grantedAt,
            expiresAt);
    }

    private static bool TryReadDateTimeOffset(JsonElement element, string propertyName, out DateTimeOffset value)
    {
        value = default;
        if (!TryGetProperty(element, propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return DateTimeOffset.TryParse(
            property.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out value);
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? ReadNullableInt64(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var value))
        {
            return value;
        }

        if (element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), out value))
        {
            return value;
        }

        return null;
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
