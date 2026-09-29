using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SafetyOpsTests.Helpers;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

[TestFixture]
public class NavigationTests : AppPageTest
{
    [Test]
    public async Task UiSignInFromSplashLandsOnHome()
    {
        await SignOutAsync();
        await LoginPage.SignInFromSplashAsync(Page, DemoUsers.Admin.Username, DemoUsers.Admin.Password);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Welcome to SAFETYOPS" })).ToBeVisibleAsync();
        await Expect(Navigation.SignedInUser(Page)).ToHaveTextAsync("Demo Admin");
    }

    [Test]
    public async Task ModulesMenuListsEveryModule()
    {
        await Navigation.ModulesLink(Page).ClickAsync();
        await Expect(Navigation.ModulesMenu(Page).GetByRole(AriaRole.Link)).ToHaveTextAsync(Navigation.Modules);
    }

    [TestCase(Navigation.Personnel, "/personnel", "Personnel")]
    [TestCase(Navigation.Training, "/training", "Training")]
    [TestCase(Navigation.MedicalSurveillance, "/medical-surveillance", "Medical Surveillance")]
    [TestCase(Navigation.IncidentReports, "/incidents", "Incident Reports")]
    public async Task ModulesMenuOpensModule(string module, string path, string heading)
    {
        await Navigation.OpenModuleAsync(Page, module);
        await Expect(Page).ToHaveURLAsync(new Regex(Regex.Escape(path) + "$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).First).ToBeVisibleAsync();
    }
}
