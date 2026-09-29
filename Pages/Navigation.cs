using Microsoft.Playwright;

namespace SafetyOpsTests.Pages;

/// <summary>The shared nav bar: the Modules menu, the signed-in user, and Sign out.</summary>
public static class Navigation
{
    public const string Personnel = "Personnel";
    public const string Training = "Training";
    public const string MedicalSurveillance = "Medical Surveillance";
    public const string IncidentReports = "Incident Reports";

    public static readonly string[] Modules = [Personnel, Training, MedicalSurveillance, IncidentReports];

    public static ILocator ModulesLink(IPage page) => page.Locator("#modules-menu");
    public static ILocator ModulesMenu(IPage page) => page.GetByRole(AriaRole.Menu);
    public static ILocator SignedInUser(IPage page) => page.GetByLabel("Signed in user");
    public static ILocator SignOutLink(IPage page) => page.GetByRole(AriaRole.Link, new() { Name = "Sign out" });

    /// <summary>Opens the Modules menu and picks a module by its menu label.</summary>
    public static async Task OpenModuleAsync(IPage page, string module)
    {
        await ModulesLink(page).ClickAsync();
        await ModulesMenu(page).GetByRole(AriaRole.Link, new() { Name = module, Exact = true }).ClickAsync();
    }
}
