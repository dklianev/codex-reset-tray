using CodexResetTray.Core.Protocol;

namespace CodexResetTray.Tests;

public sealed class ResetCreditExpiryParserTests
{
    [Fact]
    public void Parse_extracts_available_credits_sorted_by_expiry()
    {
        const string json = """
        {
          "available_count": 2,
          "credits": [
            {
              "title": "One free rate limit reset",
              "status": "available",
              "reset_type": "codex_rate_limits",
              "granted_at": "2026-06-20T12:00:00Z",
              "expires_at": "2026-07-20T12:00:00Z",
              "ignored_server_field": "must not matter"
            },
            {
              "title": "One free rate limit reset",
              "status": "available",
              "reset_type": "codex_rate_limits",
              "granted_at": "2026-06-12T08:30:00Z",
              "expires_at": "2026-07-12T08:30:00Z"
            }
          ]
        }
        """;

        var fetchedAt = DateTimeOffset.Parse("2026-06-28T10:00:00Z");

        var report = ResetCreditExpiryParser.Parse(json, fetchedAt);

        Assert.Equal(2, report.AvailableCount);
        Assert.Equal(fetchedAt, report.FetchedAt);
        Assert.Equal(2, report.Credits.Count);
        Assert.Equal("codex_rate_limits", report.Credits[0].ResetType);
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T08:30:00Z"), report.Credits[0].GrantedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-07-12T08:30:00Z"), report.Credits[0].ExpiresAt);
        Assert.Equal(DateTimeOffset.Parse("2026-07-20T12:00:00Z"), report.Credits[1].ExpiresAt);
    }

    [Fact]
    public void Parse_ignores_unavailable_or_malformed_credit_rows()
    {
        const string json = """
        {
          "available_count": 3,
          "credits": [
            {
              "title": "Used reset",
              "status": "used",
              "reset_type": "codex_rate_limits",
              "granted_at": "2026-06-01T00:00:00Z",
              "expires_at": "2026-07-01T00:00:00Z"
            },
            {
              "title": "Missing expiry",
              "status": "available",
              "reset_type": "codex_rate_limits",
              "granted_at": "2026-06-02T00:00:00Z"
            },
            {
              "title": "Valid reset",
              "status": "available",
              "reset_type": "codex_rate_limits",
              "granted_at": "2026-06-03T00:00:00Z",
              "expires_at": "2026-07-03T00:00:00Z"
            }
          ]
        }
        """;

        var report = ResetCreditExpiryParser.Parse(json, DateTimeOffset.Parse("2026-06-28T10:00:00Z"));

        var credit = Assert.Single(report.Credits);
        Assert.Equal("Valid reset", credit.Title);
        Assert.Equal(DateTimeOffset.Parse("2026-07-03T00:00:00Z"), credit.ExpiresAt);
    }
}
