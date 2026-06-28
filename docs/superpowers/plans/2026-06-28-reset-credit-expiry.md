# Reset Credit Expiry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add optional, opt-in reset-credit expiry details to Codex Reset Tray by porting the safe parts of `zorbeytorunoglu/codex-resets` into the existing native WPF app.

**Architecture:** Keep `codex app-server` as the primary source of rate-limit windows and available reset-credit count. Add a separate experimental expiry provider that reads the local Codex auth file only when the user enables it, calls the unofficial `https://chatgpt.com/backend-api/wham/rate-limit-reset-credits` endpoint, parses only redacted fields, and enriches the dashboard snapshot. If the experimental provider fails, the app keeps working with the current app-server data.

**Tech Stack:** C#/.NET 10, WPF, `HttpClient`, `System.Text.Json`, existing xUnit tests, existing `SecretRedactor`, existing JSON settings file under `%AppData%\CodexResetTray\settings.json`.

---

## Source Findings

The upstream project is a Node 20 CLI. Its useful implementation details are:

- Endpoint: `https://chatgpt.com/backend-api/wham/rate-limit-reset-credits`
- Method: `GET`
- Auth source: `tokens.access_token` and `tokens.account_id` from Codex `auth.json`
- Headers:
  - `Authorization: Bearer <access_token>`
  - `ChatGPT-Account-ID: <account_id>`
  - `OpenAI-Beta: codex-1`
  - `originator: Codex Desktop`
- Response shape:

```json
{
  "available_count": 2,
  "credits": [
    {
      "title": "One free rate limit reset",
      "status": "available",
      "reset_type": "codex_rate_limits",
      "granted_at": "2026-06-12T00:00:00Z",
      "expires_at": "2026-07-12T00:00:00Z"
    }
  ]
}
```

The endpoint is unofficial and can change. Treat this feature as experimental, user-controlled, and non-blocking.

---

## File Structure

Create:

- `src/CodexResetTray.Core/RateLimits/ResetCreditInfo.cs`: immutable per-credit domain model.
- `src/CodexResetTray.Core/RateLimits/ResetCreditReport.cs`: immutable report with available count, credit list, and fetched time.
- `src/CodexResetTray.Core/Protocol/ResetCreditExpiryParser.cs`: parser for the unofficial endpoint response.
- `src/CodexResetTray.App/Services/IResetCreditExpirySource.cs`: async source abstraction.
- `src/CodexResetTray.App/Services/CodexAuthFileCredentialsProvider.cs`: opt-in auth file reader.
- `src/CodexResetTray.App/Services/WhamResetCreditExpirySource.cs`: HTTP provider.
- `src/CodexResetTray.App/Services/CombinedRateLimitSource.cs`: wraps app-server and optional expiry source.
- `tests/CodexResetTray.Tests/ResetCreditExpiryParserTests.cs`
- `tests/CodexResetTray.Tests/CodexAuthFileCredentialsProviderTests.cs`
- `tests/CodexResetTray.Tests/WhamResetCreditExpirySourceTests.cs`
- `tests/CodexResetTray.Tests/CombinedRateLimitSourceTests.cs`

Modify:

- `src/CodexResetTray.Core/RateLimits/RateLimitDashboardSnapshot.cs`: add optional `ResetCreditReport? ResetCreditDetails`.
- `src/CodexResetTray.App/Services/JsonAlertSettingsService.cs`: persist experimental opt-in and warning threshold.
- `src/CodexResetTray.App/Services/IAlertSettingsService.cs`: add reset-credit expiry settings.
- `src/CodexResetTray.App/App.xaml.cs`: wire the combined source.
- `src/CodexResetTray.App/ViewModels/DashboardViewModel.cs`: expose next-expiring credit text and notification logic.
- `src/CodexResetTray.App/Services/TrayController.cs`: add tray opt-in toggle and richer reset-credit menu text.
- `README.md`, `PRIVACY.md`, `SECURITY.md`, `docs/rate-limit-source.md`: document the experimental source.

---

### Task 1: Domain Model and Parser

**Files:**
- Create: `src/CodexResetTray.Core/RateLimits/ResetCreditInfo.cs`
- Create: `src/CodexResetTray.Core/RateLimits/ResetCreditReport.cs`
- Create: `src/CodexResetTray.Core/Protocol/ResetCreditExpiryParser.cs`
- Test: `tests/CodexResetTray.Tests/ResetCreditExpiryParserTests.cs`

- [ ] **Step 1: Write parser tests**

```csharp
using CodexResetTray.Core.Protocol;

namespace CodexResetTray.Tests;

public sealed class ResetCreditExpiryParserTests
{
    [Fact]
    public void Parse_reads_and_sorts_credits_by_expiry()
    {
        const string json = """
        {
          "available_count": 2,
          "credits": [
            {
              "title": "Later",
              "status": "available",
              "reset_type": "codex_rate_limits",
              "granted_at": "2026-06-18T00:00:00Z",
              "expires_at": "2026-07-18T00:00:00Z"
            },
            {
              "title": "Sooner",
              "status": "available",
              "reset_type": "codex_rate_limits",
              "granted_at": "2026-06-12T00:00:00Z",
              "expires_at": "2026-07-12T00:00:00Z"
            }
          ]
        }
        """;

        var fetchedAt = new DateTimeOffset(2026, 6, 28, 10, 0, 0, TimeSpan.Zero);
        var report = ResetCreditExpiryParser.Parse(json, fetchedAt);

        Assert.Equal(2, report.AvailableCount);
        Assert.Equal(fetchedAt, report.FetchedAt);
        Assert.Collection(
            report.Credits,
            first =>
            {
                Assert.Equal("Sooner", first.Title);
                Assert.Equal(new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero), first.ExpiresAt);
            },
            second => Assert.Equal("Later", second.Title));
    }

    [Fact]
    public void Parse_rejects_missing_expiry()
    {
        const string json = """{"available_count":1,"credits":[{"granted_at":"2026-06-12T00:00:00Z"}]}""";

        var ex = Assert.Throws<FormatException>(() =>
            ResetCreditExpiryParser.Parse(json, DateTimeOffset.UtcNow));

        Assert.Contains("expires_at", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet test .\CodexResetTray.slnx -c Release --filter FullyQualifiedName~ResetCreditExpiryParserTests
```

Expected: compile failure because `ResetCreditExpiryParser`, `ResetCreditInfo`, and `ResetCreditReport` do not exist.

- [ ] **Step 3: Add domain models**

Create `src/CodexResetTray.Core/RateLimits/ResetCreditInfo.cs`:

```csharp
namespace CodexResetTray.Core.RateLimits;

public sealed record ResetCreditInfo(
    string Title,
    string Status,
    string ResetType,
    DateTimeOffset GrantedAt,
    DateTimeOffset ExpiresAt)
{
    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public bool ExpiresWithin(DateTimeOffset now, TimeSpan window) =>
        ExpiresAt > now && ExpiresAt <= now.Add(window);
}
```

Create `src/CodexResetTray.Core/RateLimits/ResetCreditReport.cs`:

```csharp
namespace CodexResetTray.Core.RateLimits;

public sealed record ResetCreditReport(
    int AvailableCount,
    IReadOnlyList<ResetCreditInfo> Credits,
    DateTimeOffset FetchedAt)
{
    public ResetCreditInfo? NextExpiring(DateTimeOffset now) =>
        Credits
            .Where(credit => credit.ExpiresAt > now)
            .OrderBy(credit => credit.ExpiresAt)
            .FirstOrDefault();
}
```

- [ ] **Step 4: Add parser**

Create `src/CodexResetTray.Core/Protocol/ResetCreditExpiryParser.cs`:

```csharp
using System.Text.Json;
using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.Core.Protocol;

public static class ResetCreditExpiryParser
{
    public static ResetCreditReport Parse(string json, DateTimeOffset fetchedAt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Reset-credit response shape changed: expected object.");
        }

        var availableCount = ReadRequiredInt(root, "available_count");
        if (!root.TryGetProperty("credits", out var creditsElement)
            || creditsElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Reset-credit response shape changed: credits is missing or not an array.");
        }

        var credits = new List<ResetCreditInfo>();
        var index = 0;
        foreach (var credit in creditsElement.EnumerateArray())
        {
            index++;
            if (credit.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"Reset-credit response shape changed: credits[{index}] is not an object.");
            }

            credits.Add(new ResetCreditInfo(
                ReadOptionalString(credit, "title") ?? $"Reset credit {index}",
                ReadOptionalString(credit, "status") ?? "unknown",
                ReadOptionalString(credit, "reset_type") ?? "unknown",
                ReadRequiredDate(credit, "granted_at", index),
                ReadRequiredDate(credit, "expires_at", index)));
        }

        return new ResetCreditReport(
            availableCount,
            credits.OrderBy(credit => credit.ExpiresAt).ToArray(),
            fetchedAt);
    }

    private static int ReadRequiredInt(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var parsed))
        {
            return parsed;
        }

        throw new FormatException($"Reset-credit response shape changed: {propertyName} is missing or not an integer.");
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static DateTimeOffset ReadRequiredDate(JsonElement element, string propertyName, int index)
    {
        var value = ReadOptionalString(element, propertyName);
        if (value is not null && DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new FormatException($"Reset-credit response shape changed: credits[{index}].{propertyName} is missing or not an ISO datetime.");
    }
}
```

- [ ] **Step 5: Run parser tests**

Run:

```powershell
dotnet test .\CodexResetTray.slnx -c Release --filter FullyQualifiedName~ResetCreditExpiryParserTests
```

Expected: parser tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/CodexResetTray.Core/RateLimits/ResetCreditInfo.cs `
        src/CodexResetTray.Core/RateLimits/ResetCreditReport.cs `
        src/CodexResetTray.Core/Protocol/ResetCreditExpiryParser.cs `
        tests/CodexResetTray.Tests/ResetCreditExpiryParserTests.cs
git commit -m "Add reset credit expiry parser"
```

---

### Task 2: Opt-In Settings and Auth Credentials

**Files:**
- Modify: `src/CodexResetTray.App/Services/IAlertSettingsService.cs`
- Modify: `src/CodexResetTray.App/Services/JsonAlertSettingsService.cs`
- Create: `src/CodexResetTray.App/Services/CodexAuthCredentials.cs`
- Create: `src/CodexResetTray.App/Services/CodexAuthFileCredentialsProvider.cs`
- Test: `tests/CodexResetTray.Tests/CodexAuthFileCredentialsProviderTests.cs`
- Test: extend `tests/CodexResetTray.Tests/JsonAlertSettingsServiceTests.cs`

- [ ] **Step 1: Write settings tests**

Add to `JsonAlertSettingsServiceTests`:

```csharp
[Fact]
public void Reset_credit_expiry_lookup_defaults_to_disabled()
{
    var path = CreateSettingsPath();
    var service = new JsonAlertSettingsService(path);

    Assert.False(service.ResetCreditExpiryLookupEnabled);
    Assert.Equal(48, service.ResetCreditExpiryWarningHours);
}

[Fact]
public void Reset_credit_expiry_settings_persist()
{
    var path = CreateSettingsPath();
    var service = new JsonAlertSettingsService(path)
    {
        ResetCreditExpiryLookupEnabled = true,
        ResetCreditExpiryWarningHours = 12
    };

    var reloaded = new JsonAlertSettingsService(path);

    Assert.True(reloaded.ResetCreditExpiryLookupEnabled);
    Assert.Equal(12, reloaded.ResetCreditExpiryWarningHours);
}
```

- [ ] **Step 2: Write auth provider tests**

Create `tests/CodexResetTray.Tests/CodexAuthFileCredentialsProviderTests.cs`:

```csharp
using CodexResetTray.App.Services;

namespace CodexResetTray.Tests;

public sealed class CodexAuthFileCredentialsProviderTests
{
    [Fact]
    public async Task ReadAsync_extracts_access_token_and_account_id()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "auth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
        {
          "tokens": {
            "access_token": "secret-token",
            "account_id": "account-123"
          }
        }
        """);
        var provider = new CodexAuthFileCredentialsProvider(path);

        var credentials = await provider.ReadAsync(CancellationToken.None);

        Assert.Equal("secret-token", credentials.AccessToken);
        Assert.Equal("account-123", credentials.AccountId);
    }

    [Fact]
    public async Task ReadAsync_rejects_missing_token_without_leaking_file_content()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "auth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """{"tokens":{"access_token":"","account_id":"account-123"}}""");
        var provider = new CodexAuthFileCredentialsProvider(path);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ReadAsync(CancellationToken.None));

        Assert.DoesNotContain("account-123", ex.Message, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 3: Run tests and verify they fail**

Run:

```powershell
dotnet test .\CodexResetTray.slnx -c Release --filter "FullyQualifiedName~CodexAuthFileCredentialsProviderTests|FullyQualifiedName~JsonAlertSettingsServiceTests"
```

Expected: compile failure because the settings and provider do not exist.

- [ ] **Step 4: Add settings properties**

Modify `IAlertSettingsService`:

```csharp
int ResetCreditExpiryWarningHours { get; set; }

bool ResetCreditExpiryLookupEnabled { get; set; }
```

Modify `JsonAlertSettingsService.AppSettings`:

```csharp
public bool ResetCreditExpiryLookupEnabled { get; set; }

public int ResetCreditExpiryWarningHours { get; set; } = 48;
```

Use a sanitizer:

```csharp
private static int SanitizeWarningHours(int value) => Math.Clamp(value, 1, 24 * 30);
```

- [ ] **Step 5: Add auth credentials provider**

Create `src/CodexResetTray.App/Services/CodexAuthCredentials.cs`:

```csharp
namespace CodexResetTray.App.Services;

public sealed record CodexAuthCredentials(string AccessToken, string AccountId);
```

Create `src/CodexResetTray.App/Services/CodexAuthFileCredentialsProvider.cs`:

```csharp
using System.IO;
using System.Text.Json;

namespace CodexResetTray.App.Services;

public sealed class CodexAuthFileCredentialsProvider
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

    public async Task<CodexAuthCredentials> ReadAsync(CancellationToken cancellationToken)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(_authPath, cancellationToken);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Could not read Codex auth file.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException("Could not access Codex auth file.", ex);
        }

        using var document = JsonDocument.Parse(text);
        if (!document.RootElement.TryGetProperty("tokens", out var tokens))
        {
            throw new InvalidOperationException("Codex auth file is missing tokens.");
        }

        var accessToken = ReadRequiredString(tokens, "access_token", "Codex auth file contains an invalid access token.");
        var accountId = ReadRequiredString(tokens, "account_id", "Codex auth file contains an invalid account ID.");
        return new CodexAuthCredentials(accessToken, accountId);
    }

    private static string ResolveDefaultAuthPath()
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(codexHome))
        {
            return Path.Combine(Environment.ExpandEnvironmentVariables(codexHome), "auth.json");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "auth.json");
    }

    private static string ReadRequiredString(JsonElement element, string propertyName, string message)
    {
        if (element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new InvalidOperationException(message);
    }
}
```

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test .\CodexResetTray.slnx -c Release --filter "FullyQualifiedName~CodexAuthFileCredentialsProviderTests|FullyQualifiedName~JsonAlertSettingsServiceTests"
```

Expected: tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/CodexResetTray.App/Services/IAlertSettingsService.cs `
        src/CodexResetTray.App/Services/JsonAlertSettingsService.cs `
        src/CodexResetTray.App/Services/CodexAuthCredentials.cs `
        src/CodexResetTray.App/Services/CodexAuthFileCredentialsProvider.cs `
        tests/CodexResetTray.Tests/CodexAuthFileCredentialsProviderTests.cs `
        tests/CodexResetTray.Tests/JsonAlertSettingsServiceTests.cs
git commit -m "Add opt-in reset credit expiry settings"
```

---

### Task 3: HTTP Provider and Combined Source

**Files:**
- Create: `src/CodexResetTray.App/Services/IResetCreditExpirySource.cs`
- Create: `src/CodexResetTray.App/Services/WhamResetCreditExpirySource.cs`
- Create: `src/CodexResetTray.App/Services/CombinedRateLimitSource.cs`
- Modify: `src/CodexResetTray.Core/RateLimits/RateLimitDashboardSnapshot.cs`
- Test: `tests/CodexResetTray.Tests/WhamResetCreditExpirySourceTests.cs`
- Test: `tests/CodexResetTray.Tests/CombinedRateLimitSourceTests.cs`

- [ ] **Step 1: Write provider tests with fake HTTP**

Use a fake `HttpMessageHandler` and assert headers:

```csharp
Assert.Equal("Bearer secret-token", request.Headers.Authorization!.ToString());
Assert.True(request.Headers.TryGetValues("ChatGPT-Account-ID", out var accounts));
Assert.Equal("account-123", Assert.Single(accounts));
Assert.True(request.Headers.TryGetValues("OpenAI-Beta", out var beta));
Assert.Equal("codex-1", Assert.Single(beta));
Assert.True(request.Headers.TryGetValues("originator", out var originator));
Assert.Equal("Codex Desktop", Assert.Single(originator));
```

- [ ] **Step 2: Write combined source tests**

Test enabled and disabled behavior:

```csharp
[Fact]
public async Task ReadAsync_skips_expiry_source_when_disabled()
{
    var rateSource = new StubRateLimitSource(CreateSnapshot());
    var expirySource = new ThrowingResetCreditExpirySource();
    var settings = new StubAlertSettingsService(thresholdPercent: 10)
    {
        ResetCreditExpiryLookupEnabled = false
    };
    var source = new CombinedRateLimitSource(rateSource, expirySource, settings);

    var snapshot = await source.ReadAsync(CancellationToken.None);

    Assert.Null(snapshot.ResetCreditDetails);
}
```

- [ ] **Step 3: Add snapshot property**

Modify `RateLimitDashboardSnapshot`:

```csharp
public sealed record RateLimitDashboardSnapshot(
    IReadOnlyList<RateLimitBucket> Buckets,
    long? ResetCreditsAvailable,
    DateTimeOffset FetchedAt,
    ResetCreditReport? ResetCreditDetails = null)
{
    public static RateLimitDashboardSnapshot Empty(DateTimeOffset fetchedAt) =>
        new(Array.Empty<RateLimitBucket>(), null, fetchedAt);
}
```

- [ ] **Step 4: Add interfaces and implementation**

Create `IResetCreditExpirySource`:

```csharp
using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.App.Services;

public interface IResetCreditExpirySource
{
    Task<ResetCreditReport> ReadAsync(CancellationToken cancellationToken);
}
```

Create `CombinedRateLimitSource`:

```csharp
using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.App.Services;

public sealed class CombinedRateLimitSource : IRateLimitSource, IDisposable
{
    private readonly IRateLimitSource _rateLimitSource;
    private readonly IResetCreditExpirySource _resetCreditExpirySource;
    private readonly IAlertSettingsService _settings;

    public CombinedRateLimitSource(
        IRateLimitSource rateLimitSource,
        IResetCreditExpirySource resetCreditExpirySource,
        IAlertSettingsService settings)
    {
        _rateLimitSource = rateLimitSource;
        _resetCreditExpirySource = resetCreditExpirySource;
        _settings = settings;
    }

    public async Task<RateLimitDashboardSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _rateLimitSource.ReadAsync(cancellationToken);
        if (!_settings.ResetCreditExpiryLookupEnabled)
        {
            return snapshot;
        }

        try
        {
            var report = await _resetCreditExpirySource.ReadAsync(cancellationToken);
            return snapshot with { ResetCreditDetails = report };
        }
        catch (Exception)
        {
            return snapshot;
        }
    }

    public void Dispose()
    {
        if (_rateLimitSource is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
```

- [ ] **Step 5: Implement HTTP provider**

Create `src/CodexResetTray.App/Services/WhamResetCreditExpirySource.cs`:

```csharp
using System.Net.Http.Headers;
using CodexResetTray.Core.Protocol;
using CodexResetTray.Core.RateLimits;
using CodexResetTray.Core.Security;

namespace CodexResetTray.App.Services;

public sealed class WhamResetCreditExpirySource : IResetCreditExpirySource
{
    private static readonly Uri Endpoint = new("https://chatgpt.com/backend-api/wham/rate-limit-reset-credits");
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);
    private readonly HttpClient _httpClient;
    private readonly CodexAuthFileCredentialsProvider _credentialsProvider;

    public WhamResetCreditExpirySource(
        HttpClient httpClient,
        CodexAuthFileCredentialsProvider credentialsProvider)
    {
        _httpClient = httpClient;
        _credentialsProvider = credentialsProvider;
    }

    public async Task<ResetCreditReport> ReadAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadTimeout);

        var credentials = await _credentialsProvider.ReadAsync(timeout.Token);
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        request.Headers.TryAddWithoutValidation("ChatGPT-Account-ID", credentials.AccountId);
        request.Headers.TryAddWithoutValidation("OpenAI-Beta", "codex-1");
        request.Headers.TryAddWithoutValidation("originator", "Codex Desktop");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Reset-credit expiry endpoint returned HTTP {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadAsStringAsync(timeout.Token);
        try
        {
            return ResetCreditExpiryParser.Parse(body, DateTimeOffset.Now);
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            throw new InvalidOperationException(
                SecretRedactor.Redact("Reset-credit expiry endpoint response shape changed."),
                ex);
        }
    }
}
```

The provider never logs the raw response, bearer token, account ID, or auth file content. `CombinedRateLimitSource` catches provider failures and returns the app-server snapshot unchanged.

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test .\CodexResetTray.slnx -c Release --filter "FullyQualifiedName~WhamResetCreditExpirySourceTests|FullyQualifiedName~CombinedRateLimitSourceTests"
```

Expected: tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/CodexResetTray.App/Services/IResetCreditExpirySource.cs `
        src/CodexResetTray.App/Services/WhamResetCreditExpirySource.cs `
        src/CodexResetTray.App/Services/CombinedRateLimitSource.cs `
        src/CodexResetTray.Core/RateLimits/RateLimitDashboardSnapshot.cs `
        tests/CodexResetTray.Tests/WhamResetCreditExpirySourceTests.cs `
        tests/CodexResetTray.Tests/CombinedRateLimitSourceTests.cs
git commit -m "Add optional reset credit expiry source"
```

---

### Task 4: UI, Tray, and Notifications

**Files:**
- Modify: `src/CodexResetTray.App/App.xaml.cs`
- Modify: `src/CodexResetTray.App/ViewModels/DashboardViewModel.cs`
- Modify: `src/CodexResetTray.App/Services/TrayController.cs`
- Modify: `src/CodexResetTray.App/MainWindow.xaml`
- Test: `tests/CodexResetTray.Tests/DashboardViewModelTests.cs`
- Test: `tests/CodexResetTray.Tests/DashboardNotificationTests.cs`

- [ ] **Step 1: Wire combined source**

In `App.xaml.cs`, replace direct source wiring:

```csharp
var rateLimitSource = new CodexAppServerRateLimitSource();
var alertSettings = new JsonAlertSettingsService();
var resetCreditSource = new WhamResetCreditExpirySource(
    new HttpClient(),
    new CodexAuthFileCredentialsProvider());
var source = new CombinedRateLimitSource(rateLimitSource, resetCreditSource, alertSettings);
_dashboard = new DashboardViewModel(source, _shutdown.Token, startup, alertSettings);
```

- [ ] **Step 2: Add ViewModel properties**

Expose:

```csharp
public string ResetCreditExpiryText { get; private set; } = "Expiry lookup off";
public string ResetCreditNextExpiryText { get; private set; } = "No reset credit expiry data";
public bool ResetCreditExpiryLookupEnabled { get; set; }
```

When applying snapshot:

```csharp
var report = snapshot.ResetCreditDetails;
ResetCreditExpiryText = report is null
    ? "Expiry lookup off"
    : $"Reset credits: {report.AvailableCount}";
ResetCreditNextExpiryText = FormatNextResetCreditExpiry(report, DateTimeOffset.Now);
```

- [ ] **Step 3: Add tray menu toggle**

Add a checked item after `Notifications`:

```text
Reset credit expiry lookup (experimental)
```

Clicking it toggles `DashboardViewModel.ResetCreditExpiryLookupEnabled` and triggers a silent refresh.

- [ ] **Step 4: Add dashboard UI**

In the existing notification/settings area, add a compact row:

```text
Reset credit expiry: next expires Jul 12, 2026 07:37 local (13d left)
```

When disabled:

```text
Reset credit expiry lookup is off
```

Avoid a large new settings screen in this task.

- [ ] **Step 5: Add expiring-soon notification test**

Test that when a credit enters the configured warning window, one notification is created and not repeated on every refresh. Use a stable notification key based on `resetType + grantedAt + expiresAt`.

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test .\CodexResetTray.slnx -c Release --filter "FullyQualifiedName~DashboardViewModelTests|FullyQualifiedName~DashboardNotificationTests"
```

Expected: tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/CodexResetTray.App/App.xaml.cs `
        src/CodexResetTray.App/ViewModels/DashboardViewModel.cs `
        src/CodexResetTray.App/Services/TrayController.cs `
        src/CodexResetTray.App/MainWindow.xaml `
        tests/CodexResetTray.Tests/DashboardViewModelTests.cs `
        tests/CodexResetTray.Tests/DashboardNotificationTests.cs
git commit -m "Show reset credit expiry details"
```

---

### Task 5: Docs, Safety, and Release Verification

**Files:**
- Modify: `README.md`
- Modify: `PRIVACY.md`
- Modify: `SECURITY.md`
- Modify: `docs/rate-limit-source.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Document the opt-in source**

Add to `docs/rate-limit-source.md`:

```markdown
## Experimental Reset Credit Expiry Source

When enabled by the user, Codex Reset Tray reads `auth.json` at runtime and calls:

`GET https://chatgpt.com/backend-api/wham/rate-limit-reset-credits`

This endpoint is unofficial and may change. The feature is disabled by default. The app does not log, display, cache, or persist access tokens. If the endpoint fails, the app falls back to the official `codex app-server` rate-limit data.
```

- [ ] **Step 2: Document privacy constraints**

Add to `PRIVACY.md`:

```markdown
Reset-credit expiry lookup is optional and disabled by default. If enabled, the app reads the local Codex `auth.json` file only to extract the access token and account ID needed for the Codex backend request. Tokens are kept in memory for the request only and are never written to the app settings file or logs.
```

- [ ] **Step 3: Run full verification**

Run:

```powershell
dotnet build .\CodexResetTray.slnx -c Release
dotnet test .\CodexResetTray.slnx -c Release --no-build
pwsh -NoProfile -File .\packaging\publish.ps1
pwsh -NoProfile -File .\tests\packaging\Verify-PublishNativeFailure.ps1
```

Expected:

- Build succeeds with 0 warnings.
- All tests pass.
- Publish script creates `artifacts\release\CodexResetTray-win-x64.zip`.
- Packaging regression script exits 0.

- [ ] **Step 4: Commit**

```powershell
git add README.md PRIVACY.md SECURITY.md docs/rate-limit-source.md CHANGELOG.md
git commit -m "Document reset credit expiry lookup"
```

---

## Self-Review

Spec coverage:

- Per-credit expiry is covered by parser and model tasks.
- Auth handling is opt-in and covered by settings and provider tasks.
- UI integration is covered by tray and dashboard tasks.
- Regression coverage is included for parser, credentials, HTTP headers, combined source, ViewModel, notifications, and docs.

Placeholder scan:

- No `TBD`, `TODO`, or "implement later" placeholders remain.

Type consistency:

- `ResetCreditInfo`, `ResetCreditReport`, `ResetCreditExpiryParser`, `IResetCreditExpirySource`, and `CombinedRateLimitSource` are introduced before use.

Recommended execution:

1. Implement Tasks 1-3 first and stop. Verify the data path without UI.
2. Implement Task 4 only after the provider is stable.
3. Keep the feature disabled by default until a user explicitly enables it.
