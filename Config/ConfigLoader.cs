using Microsoft.Extensions.Configuration;

namespace SafetyOpsTests.Config;

/// <summary>
/// Loads <see cref="TestSettings"/> from appsettings.json, then environment variables
/// (<c>TestSettings__BaseUrl</c>, <c>TestSettings__Username</c>, <c>TestSettings__Password</c>, <c>TestSettings__Headless</c>).
/// </summary>
public static class ConfigLoader
{
    private static TestSettings? _settings;

    public static TestSettings Settings => _settings ??= Load();

    private static TestSettings Load()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var settings = new TestSettings();
        config.GetSection("TestSettings").Bind(settings);

        if (string.IsNullOrWhiteSpace(settings.BaseUrl)) throw new InvalidOperationException("TestSettings:BaseUrl is not set.");
        if (string.IsNullOrEmpty(settings.Username) || string.IsNullOrEmpty(settings.Password))
            throw new InvalidOperationException("TestSettings:Username or TestSettings:Password is not set.");

        settings.BaseUrl = settings.BaseUrl.TrimEnd('/');
        return settings;
    }
}
