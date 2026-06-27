using System.Text.Json;
using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.Core.Protocol;

public static class AppServerRateLimitParser
{
    public static RateLimitDashboardSnapshot Parse(string json, DateTimeOffset fetchedAt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        long? resetCredits = TryGetProperty(root, "rateLimitResetCredits", out var creditsElement)
            && creditsElement.ValueKind == JsonValueKind.Object
            && TryGetProperty(creditsElement, "availableCount", out var availableCount)
            ? ReadNullableInt64(availableCount)
            : null;

        var buckets = new List<RateLimitBucket>();

        if (TryGetProperty(root, "rateLimitsByLimitId", out var byLimitId)
            && byLimitId.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in byLimitId.EnumerateObject().OrderBy(prop => prop.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    buckets.Add(ParseBucket(property.Value, fallbackLimitId: property.Name));
                }
            }
        }

        if (buckets.Count == 0
            && TryGetProperty(root, "rateLimits", out var rateLimits)
            && rateLimits.ValueKind == JsonValueKind.Object)
        {
            buckets.Add(ParseBucket(rateLimits, fallbackLimitId: "codex"));
        }

        return new RateLimitDashboardSnapshot(buckets, resetCredits, fetchedAt);
    }

    private static RateLimitBucket ParseBucket(JsonElement element, string fallbackLimitId)
    {
        var limitId = ReadString(element, "limitId") ?? fallbackLimitId;
        var limitName = ReadString(element, "limitName");
        var displayName = string.IsNullOrWhiteSpace(limitName)
            ? HumanizeLimitId(limitId)
            : limitName;

        return new RateLimitBucket(
            limitId,
            displayName,
            ReadString(element, "planType"),
            ReadString(element, "rateLimitReachedType"),
            ReadWindow(element, "primary"),
            ReadWindow(element, "secondary"));
    }

    private static RateLimitWindowInfo? ReadWindow(JsonElement parent, string propertyName)
    {
        if (!TryGetProperty(parent, propertyName, out var element) || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var usedPercent = ReadInt32(element, "usedPercent") ?? 0;
        var duration = ReadInt32(element, "windowDurationMins");
        var resetsAtUnix = ReadInt64(element, "resetsAt");
        var resetsAt = resetsAtUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(resetsAtUnix.Value) : (DateTimeOffset?)null;

        return new RateLimitWindowInfo(ClassifyWindow(duration), usedPercent, duration, resetsAt);
    }

    private static RateLimitWindowKind ClassifyWindow(int? durationMinutes) => durationMinutes switch
    {
        300 => RateLimitWindowKind.FiveHour,
        10080 => RateLimitWindowKind.Weekly,
        _ => RateLimitWindowKind.Unknown
    };

    private static string HumanizeLimitId(string limitId)
    {
        if (string.Equals(limitId, "codex", StringComparison.OrdinalIgnoreCase))
        {
            return "Codex";
        }

        return limitId.Replace("_", " ", StringComparison.Ordinal).Trim();
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt32(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) ? ReadNullableInt32(value) : null;

    private static long? ReadInt64(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) ? ReadNullableInt64(value) : null;

    private static int? ReadNullableInt32(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value))
        {
            return value;
        }

        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out value))
        {
            return value;
        }

        return null;
    }

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
