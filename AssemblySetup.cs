using Microsoft.Playwright;
using SafetyOpsTests.Config;
using SafetyOpsTests.Helpers;

/// <summary>Runs once for the whole assembly (no namespace, so it covers every fixture).</summary>
[SetUpFixture]
public class AssemblySetup
{
    private static readonly string[] CountedLists =
    [
        "/api/personnel", "/api/training/classes", "/api/medical-surveillance/appointments", "/api/incidents",
    ];

    private IPlaywright? _playwright;
    private IAPIRequestContext? _api;
    private Dictionary<string, int> _before = [];

    [OneTimeSetUp]
    public async Task StartRun()
    {
        // Microsoft.Playwright.NUnit launches headed when HEADED=1. Respect an explicit value.
        if (Environment.GetEnvironmentVariable("HEADED") is null && !ConfigLoader.Settings.Headless)
            Environment.SetEnvironmentVariable("HEADED", "1");

        _playwright = await Playwright.CreateAsync();
        _api = await _playwright.APIRequest.NewContextAsync(new() { BaseURL = ConfigLoader.Settings.BaseUrl, IgnoreHTTPSErrors = true });
        await ApiSession.SignInAsync(_api, DemoUsers.Admin);
        _before = await CountRecordsAsync(_api);
    }

    /// <summary>
    /// Every test cleans up after itself, so the run must leave the same number of records it found.
    /// A difference means some test leaked data (or deleted data it didn't create).
    /// </summary>
    [OneTimeTearDown]
    public async Task CheckNothingLeaked()
    {
        try
        {
            var after = await CountRecordsAsync(_api!);
            var changed = _before.Where(kv => after[kv.Key] != kv.Value).Select(kv => $"{kv.Key}: {kv.Value} → {after[kv.Key]}").ToList();
            Assert.That(changed, Is.Empty, "The test run changed the number of records: " + string.Join("; ", changed));
        }
        finally
        {
            if (_api is not null) await _api.DisposeAsync();
            _playwright?.Dispose();
        }
    }

    private static async Task<Dictionary<string, int>> CountRecordsAsync(IAPIRequestContext api)
    {
        var counts = new Dictionary<string, int>();
        foreach (var url in CountedLists)
            counts[url] = (await AppApi.GetAsync<Paged<object>>(api, $"{url}?pageSize=1")).TotalCount;
        counts["/api/access/assignments"] = (await AppApi.ListRoleAssignmentsAsync(api)).Count;
        return counts;
    }
}
