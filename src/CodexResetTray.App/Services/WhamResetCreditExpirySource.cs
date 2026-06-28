using System.Net.Http;
using System.Net.Http.Headers;
using CodexResetTray.Core.Protocol;
using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.App.Services;

public sealed class WhamResetCreditExpirySource : IResetCreditExpirySource
{
    private static readonly Uri Endpoint = new("https://chatgpt.com/backend-api/wham/rate-limit-reset-credits");
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);
    private readonly HttpClient _httpClient;
    private readonly ICodexAuthCredentialsProvider _credentialsProvider;

    public WhamResetCreditExpirySource(HttpClient httpClient, ICodexAuthCredentialsProvider credentialsProvider)
    {
        _httpClient = httpClient;
        _credentialsProvider = credentialsProvider;
    }

    public async Task<ResetCreditReport> ReadAsync(DateTimeOffset fetchedAt, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadTimeout);

        var credentials = _credentialsProvider.Read();
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        request.Headers.TryAddWithoutValidation("ChatGPT-Account-ID", credentials.AccountId);
        request.Headers.TryAddWithoutValidation("OpenAI-Beta", "codex-1");
        request.Headers.TryAddWithoutValidation("originator", "Codex Desktop");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Reset-credit expiry endpoint returned HTTP {(int)response.StatusCode}.");
        }

        var json = await response.Content.ReadAsStringAsync(timeout.Token);
        return ResetCreditExpiryParser.Parse(json, fetchedAt);
    }
}
