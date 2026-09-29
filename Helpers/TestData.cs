namespace SafetyOpsTests.Helpers;

public static class TestData
{
    /// <summary>A short token, unique per call, to name test data and find it again (e.g. "qa3f9c1b2e").</summary>
    public static string UniqueToken() => "qa" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>The app rejects names longer than this ("... maximum length of 100.").</summary>
    public const int MaxNameLength = 100;
}
