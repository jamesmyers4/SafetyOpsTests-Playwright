using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework.Interfaces;
using SafetyOpsTests.Config;
using SafetyOpsTests.Helpers;

namespace SafetyOpsTests.Tests;

/// <summary>
/// Base fixture: points the browser context at <see cref="TestSettings.BaseUrl"/>, signs in
/// through the API, and opens <see cref="StartPath"/>. Override the virtual members to sign in
/// as someone else, stay signed out, or start elsewhere.
///
/// Each test also gets <see cref="Api"/>, an API session of its own signed in as Admin (unaffected
/// by the browser's user), and <see cref="Data"/>, which creates test data through it and deletes
/// that data after the test, pass or fail.
/// </summary>
public abstract class AppPageTest : PageTest
{
    private IAPIRequestContext? _api;
    private TestDataScope? _data;

    protected static TestSettings Settings => ConfigLoader.Settings;

    /// <summary>Who each test signs in as, or null to stay signed out.</summary>
    protected virtual Credentials? SignInAs => DemoUsers.Admin;

    /// <summary>Page opened after sign-in, or null to leave the tab blank.</summary>
    protected virtual string? StartPath => "/home";

    /// <summary>Admin API session for arranging, verifying, and cleaning up data.</summary>
    protected IAPIRequestContext Api => _api ?? throw new InvalidOperationException("Api is available from SetUp on.");

    /// <summary>This test's data: create through it, or put <see cref="TestDataScope.Token"/> in UI-created records.</summary>
    protected TestDataScope Data => _data ?? throw new InvalidOperationException("Data is available from SetUp on.");

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = Settings.BaseUrl,
        IgnoreHTTPSErrors = true, // the dev profile serves https://localhost with a dev certificate
    };

    /// <summary>Where failed tests leave a Playwright trace and a screenshot (uploaded by CI).</summary>
    public static string ArtifactsDirectory => Path.Combine(TestContext.CurrentContext.WorkDirectory, "playwright-traces");

    [SetUp]
    public async Task SignInAndOpenStartPage()
    {
        await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });

        _api = await Playwright.APIRequest.NewContextAsync(new() { BaseURL = Settings.BaseUrl, IgnoreHTTPSErrors = true });
        await ApiSession.SignInAsync(_api, DemoUsers.Admin);
        _data = new TestDataScope(_api);

        if (SignInAs is { } user)
            await ApiSession.SignInAsync(Context, user);
        if (StartPath is { } path)
            await Page.GotoAsync(path);
    }

    [TearDown]
    public async Task CleanUpTestData()
    {
        try
        {
            await SaveTraceIfFailedAsync();
            if (_data is not null) await _data.CleanUpAsync();
        }
        finally
        {
            if (_api is not null) await _api.DisposeAsync();
            _data = null;
            _api = null;
        }
    }

    /// <summary>Keeps the trace (open with <c>playwright.ps1 show-trace</c>) and a screenshot only for failed tests.</summary>
    private async Task SaveTraceIfFailedAsync()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status is not (TestStatus.Failed or TestStatus.Warning))
        {
            await Context.Tracing.StopAsync();
            return;
        }

        var name = string.Concat(TestContext.CurrentContext.Test.FullName.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '"' ? '_' : c));
        Directory.CreateDirectory(ArtifactsDirectory);
        await Context.Tracing.StopAsync(new() { Path = Path.Combine(ArtifactsDirectory, name + ".zip") });
        try { await Page.ScreenshotAsync(new() { Path = Path.Combine(ArtifactsDirectory, name + ".png"), FullPage = true }); }
        catch (PlaywrightException) { /* the page may already be gone */ }
        TestContext.Out.WriteLine($"Trace and screenshot saved to {ArtifactsDirectory}{Path.DirectorySeparatorChar}{name}.*");
    }

    /// <summary>Signs this test's browser context out and back in as <paramref name="user"/>.</summary>
    protected async Task SwitchUserAsync(Credentials user)
    {
        await Context.ClearCookiesAsync();
        await ApiSession.SignInAsync(Context, user);
    }

    /// <summary>A second, independent browser session signed in as <paramref name="user"/>.</summary>
    protected async Task<IPage> OpenPageAsAsync(Credentials user)
    {
        var context = await NewContext(ContextOptions());
        await ApiSession.SignInAsync(context, user);
        return await context.NewPageAsync();
    }
}
