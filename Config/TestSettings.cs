namespace SafetyOpsTests.Config;

public class TestSettings
{
    /// <summary>Root of the running SafetyOpsApp, e.g. http://localhost:8080 (Docker) or https://localhost:14418 (dev).</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>Account most tests sign in as. Defaults to the seeded demo Admin.</summary>
    public string Username { get; set; } = "admin";

    public string Password { get; set; } = "admin";

    /// <summary>Run the browser headless. The HEADED=1 environment variable still wins.</summary>
    public bool Headless { get; set; } = true;
}
