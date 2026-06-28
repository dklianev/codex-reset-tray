using System.Net;
using System.Net.Http.Headers;
using CodexResetTray.App.Services;
using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.Tests;

public sealed class ResetCreditExpirySourceTests
{
    [Fact]
    public void CodexAuthFileCredentialsProvider_reads_tokens_from_auth_json()
    {
        var path = CreateTempFile("""
        {
          "tokens": {
            "access_token": "eyJtest.access.token",
            "account_id": "acc-test-123"
          }
        }
        """);
        var provider = new CodexAuthFileCredentialsProvider(path);

        var credentials = provider.Read();

        Assert.Equal("eyJtest.access.token", credentials.AccessToken);
        Assert.Equal("acc-test-123", credentials.AccountId);
    }

    [Fact]
    public void CodexAuthFileCredentialsProvider_throws_redacted_error_for_invalid_auth_json()
    {
        var path = CreateTempFile("""
        {
          "tokens": {
            "access_token": "eyJsecret.access.token"
          }
        }
        """);
        var provider = new CodexAuthFileCredentialsProvider(path);

        var ex = Assert.Throws<InvalidOperationException>(() => provider.Read());

        Assert.DoesNotContain("eyJsecret", ex.Message);
        Assert.DoesNotContain("access_token", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("account_id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhamResetCreditExpirySource_sends_expected_read_request_and_parses_response()
    {
        HttpRequestMessage? capturedRequest = null;
        using var http = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "available_count": 1,
                  "credits": [
                    {
                      "title": "One free rate limit reset",
                      "status": "available",
                      "reset_type": "codex_rate_limits",
                      "granted_at": "2026-06-12T08:30:00Z",
                      "expires_at": "2026-07-12T08:30:00Z"
                    }
                  ]
                }
                """)
            };
        }));
        var source = new WhamResetCreditExpirySource(
            http,
            new StubCredentialsProvider(new CodexAuthCredentials("secret-token", "acc-test")));

        var report = await source.ReadAsync(DateTimeOffset.Parse("2026-06-28T10:00:00Z"), CancellationToken.None);

        Assert.Equal(1, report.AvailableCount);
        Assert.Equal(new Uri("https://chatgpt.com/backend-api/wham/rate-limit-reset-credits"), capturedRequest!.RequestUri);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "secret-token"), capturedRequest.Headers.Authorization);
        Assert.True(capturedRequest.Headers.TryGetValues("ChatGPT-Account-ID", out var accountIds));
        Assert.Equal("acc-test", Assert.Single(accountIds));
        Assert.True(capturedRequest.Headers.TryGetValues("OpenAI-Beta", out var betaValues));
        Assert.Equal("codex-1", Assert.Single(betaValues));
        Assert.True(capturedRequest.Headers.TryGetValues("originator", out var originators));
        Assert.Equal("Codex Desktop", Assert.Single(originators));
    }

    [Fact]
    public async Task WhamResetCreditExpirySource_redacts_http_failure_details()
    {
        using var http = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("secret-token should never be surfaced")
        }));
        var source = new WhamResetCreditExpirySource(
            http,
            new StubCredentialsProvider(new CodexAuthCredentials("secret-token", "acc-test")));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => source.ReadAsync(DateTimeOffset.Parse("2026-06-28T10:00:00Z"), CancellationToken.None));

        Assert.Contains("HTTP 403", ex.Message);
        Assert.DoesNotContain("secret-token", ex.Message);
    }

    [Fact]
    public async Task ResetCreditExpiryRateLimitSource_skips_expiry_endpoint_when_disabled()
    {
        var baseSnapshot = CreateSnapshot(credits: 5);
        var expiry = new StubResetCreditExpirySource(CreateReport(availableCount: 2));
        var settings = new StubAlertSettingsService(thresholdPercent: 10)
        {
            ResetCreditExpiryLookupEnabled = false
        };
        var source = new ResetCreditExpiryRateLimitSource(
            new StubRateLimitSource(baseSnapshot),
            expiry,
            settings);

        var snapshot = await source.ReadAsync(CancellationToken.None);

        Assert.Equal(5, snapshot.ResetCreditsAvailable);
        Assert.Null(snapshot.ResetCreditDetails);
        Assert.Equal(0, expiry.Calls);
    }

    [Fact]
    public async Task ResetCreditExpiryRateLimitSource_attaches_metadata_without_overriding_app_server_count()
    {
        var baseSnapshot = CreateSnapshot(credits: 5);
        var report = CreateReport(availableCount: 2);
        var settings = new StubAlertSettingsService(thresholdPercent: 10)
        {
            ResetCreditExpiryLookupEnabled = true
        };
        var source = new ResetCreditExpiryRateLimitSource(
            new StubRateLimitSource(baseSnapshot),
            new StubResetCreditExpirySource(report),
            settings);

        var snapshot = await source.ReadAsync(CancellationToken.None);

        Assert.Equal(5, snapshot.ResetCreditsAvailable);
        Assert.Same(report, snapshot.ResetCreditDetails);
    }

    [Fact]
    public async Task ResetCreditExpiryRateLimitSource_falls_back_to_base_snapshot_on_expiry_failure()
    {
        var baseSnapshot = CreateSnapshot(credits: 5);
        var settings = new StubAlertSettingsService(thresholdPercent: 10)
        {
            ResetCreditExpiryLookupEnabled = true
        };
        var source = new ResetCreditExpiryRateLimitSource(
            new StubRateLimitSource(baseSnapshot),
            new ThrowingResetCreditExpirySource(),
            settings);

        var snapshot = await source.ReadAsync(CancellationToken.None);

        Assert.Same(baseSnapshot, snapshot);
    }

    private static RateLimitDashboardSnapshot CreateSnapshot(long? credits)
    {
        var fetchedAt = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
        return new RateLimitDashboardSnapshot(
            new[]
            {
                new RateLimitBucket(
                    "codex",
                    "Codex",
                    "pro",
                    null,
                    new RateLimitWindowInfo(RateLimitWindowKind.FiveHour, 10, 300, fetchedAt.AddHours(1)),
                    new RateLimitWindowInfo(RateLimitWindowKind.Weekly, 20, 10080, fetchedAt.AddDays(3)))
            },
            credits,
            fetchedAt);
    }

    private static ResetCreditReport CreateReport(long availableCount) =>
        new(
            availableCount,
            new[]
            {
                new ResetCreditInfo(
                    "One free rate limit reset",
                    "available",
                    "codex_rate_limits",
                    DateTimeOffset.Parse("2026-06-12T08:30:00Z"),
                    DateTimeOffset.Parse("2026-07-12T08:30:00Z"))
            },
            DateTimeOffset.Parse("2026-06-28T10:00:00Z"));

    private static string CreateTempFile(string contents)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "CodexResetTrayTests",
            Guid.NewGuid().ToString("N"),
            "auth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    private sealed class StubCredentialsProvider : ICodexAuthCredentialsProvider
    {
        private readonly CodexAuthCredentials _credentials;

        public StubCredentialsProvider(CodexAuthCredentials credentials)
        {
            _credentials = credentials;
        }

        public CodexAuthCredentials Read() => _credentials;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }

    private sealed class StubRateLimitSource : IRateLimitSource
    {
        private readonly RateLimitDashboardSnapshot _snapshot;

        public StubRateLimitSource(RateLimitDashboardSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<RateLimitDashboardSnapshot> ReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_snapshot);
    }

    private sealed class StubResetCreditExpirySource : IResetCreditExpirySource
    {
        private readonly ResetCreditReport _report;

        public StubResetCreditExpirySource(ResetCreditReport report)
        {
            _report = report;
        }

        public int Calls { get; private set; }

        public Task<ResetCreditReport> ReadAsync(DateTimeOffset fetchedAt, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_report);
        }
    }

    private sealed class ThrowingResetCreditExpirySource : IResetCreditExpirySource
    {
        public Task<ResetCreditReport> ReadAsync(DateTimeOffset fetchedAt, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Endpoint unavailable");
    }
}
