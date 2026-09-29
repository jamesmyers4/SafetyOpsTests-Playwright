using SafetyOpsTests.Config;

namespace SafetyOpsTests.Helpers;

public record Credentials(string Username, string Password);

/// <summary>
/// The accounts SafetyOpsApp seeds on a fresh database. These are public demo logins, not secrets.
/// </summary>
public static class DemoUsers
{
    /// <summary>Admin on the whole organization (from TestSettings, so it can be overridden).</summary>
    public static Credentials Admin => new(ConfigLoader.Settings.Username, ConfigLoader.Settings.Password);

    /// <summary>Manager on Manufacturing Division (North Plant + South Plant).</summary>
    public static readonly Credentials Manager = new("manager", "manager");

    /// <summary>Read-only Viewer on North Plant.</summary>
    public static readonly Credentials Viewer = new("viewer", "viewer");
}
