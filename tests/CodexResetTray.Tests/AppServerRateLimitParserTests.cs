using CodexResetTray.Core.Protocol;
using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.Tests;

public sealed class AppServerRateLimitParserTests
{
    [Fact]
    public void ParseRateLimitsResponse_extracts_reset_windows_and_reset_credit_count()
    {
        const string json = """
        {
          "rateLimitResetCredits": { "availableCount": 5 },
          "rateLimits": {
            "limitId": "codex",
            "limitName": null,
            "primary": { "usedPercent": 25, "windowDurationMins": 300, "resetsAt": 1782568614 },
            "secondary": { "usedPercent": 7, "windowDurationMins": 10080, "resetsAt": 1782947742 },
            "credits": { "hasCredits": false, "unlimited": false, "balance": "0" },
            "individualLimit": null,
            "planType": "prolite",
            "rateLimitReachedType": null
          },
          "rateLimitsByLimitId": {
            "codex_bengalfox": {
              "limitId": "codex_bengalfox",
              "limitName": "GPT-5.3-Codex-Spark",
              "primary": { "usedPercent": 0, "windowDurationMins": 300, "resetsAt": 1782572118 },
              "secondary": { "usedPercent": 0, "windowDurationMins": 10080, "resetsAt": 1783158918 },
              "credits": null,
              "individualLimit": null,
              "planType": "prolite",
              "rateLimitReachedType": null
            },
            "codex": {
              "limitId": "codex",
              "limitName": null,
              "primary": { "usedPercent": 25, "windowDurationMins": 300, "resetsAt": 1782568614 },
              "secondary": { "usedPercent": 7, "windowDurationMins": 10080, "resetsAt": 1782947742 },
              "credits": { "hasCredits": false, "unlimited": false, "balance": "0" },
              "individualLimit": null,
              "planType": "prolite",
              "rateLimitReachedType": null
            }
          }
        }
        """;

        var snapshot = AppServerRateLimitParser.Parse(json, DateTimeOffset.FromUnixTimeSeconds(1782557718));

        Assert.Equal(5, snapshot.ResetCreditsAvailable);
        Assert.Equal(2, snapshot.Buckets.Count);

        var codex = snapshot.Buckets.Single(bucket => bucket.LimitId == "codex");
        Assert.Equal("Codex", codex.DisplayName);
        Assert.Equal("prolite", codex.PlanType);
        Assert.Equal(25, codex.Primary!.UsedPercent);
        Assert.Equal(RateLimitWindowKind.FiveHour, codex.Primary.Kind);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1782568614), codex.Primary.ResetsAt);
        Assert.Equal(7, codex.Secondary!.UsedPercent);
        Assert.Equal(RateLimitWindowKind.Weekly, codex.Secondary.Kind);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1782947742), codex.Secondary.ResetsAt);

        var spark = snapshot.Buckets.Single(bucket => bucket.LimitId == "codex_bengalfox");
        Assert.Equal("GPT-5.3-Codex-Spark", spark.DisplayName);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1782572118), spark.Primary!.ResetsAt);
    }

    [Fact]
    public void ParseRateLimitsResponse_falls_back_to_single_bucket_when_multi_bucket_view_is_missing()
    {
        const string json = """
        {
          "rateLimitResetCredits": null,
          "rateLimits": {
            "limitId": "codex",
            "limitName": null,
            "primary": { "usedPercent": 91, "windowDurationMins": 300, "resetsAt": 1782568614 },
            "secondary": null,
            "credits": null,
            "individualLimit": null,
            "planType": "plus",
            "rateLimitReachedType": "primary"
          },
          "rateLimitsByLimitId": null
        }
        """;

        var snapshot = AppServerRateLimitParser.Parse(json, DateTimeOffset.FromUnixTimeSeconds(1782557718));

        var bucket = Assert.Single(snapshot.Buckets);
        Assert.Equal("codex", bucket.LimitId);
        Assert.Equal("Limited", bucket.StateLabel);
        Assert.Equal(91, bucket.Primary!.UsedPercent);
        Assert.Null(snapshot.ResetCreditsAvailable);
    }
}
