using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SafetyOpsTests.Helpers;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

/// <summary>Sign-in guard, return URLs, bad credentials, and sign-out. Tests start signed out.</summary>
[TestFixture]
public class AuthTests : AppPageTest
{
    protected override Credentials? SignInAs => null;
    protected override string? StartPath => null;

    [TestCase("/training")]
    [TestCase("/personnel/edit")]
    [TestCase("/incidents/new")]
    public async Task SignedOutVisitRedirectsToLoginWithReturnUrl(string path)
    {
        await Page.GotoAsync(path);
        await Expect(Page).ToHaveURLAsync(new Regex($"/login\\?returnUrl={Regex.Escape(Uri.EscapeDataString(path))}$"));
        await Expect(LoginPage.UsernameInput(Page)).ToBeVisibleAsync();
    }

    [Test]
    public async Task FramePagesRequireSignInToo()
    {
        await Page.GotoAsync("/medical-surveillance/create-frame");
        await Expect(Page).ToHaveURLAsync(new Regex(@"/login\?returnUrl=%2Fmedical-surveillance%2Fcreate-frame$"));
    }

    [Test]
    public async Task SignInReturnsToTheRequestedPage()
    {
        await Page.GotoAsync("/personnel/edit");
        await LoginPage.SubmitAsync(Page, DemoUsers.Admin.Username, DemoUsers.Admin.Password);
        await Expect(Page).ToHaveURLAsync(new Regex("/personnel/edit$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Edit / Search User" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task WrongPasswordShowsErrorAndStaysOnLogin()
    {
        await Page.GotoAsync("/login");
        await LoginPage.SubmitAsync(Page, DemoUsers.Admin.Username, "not-the-password");
        await Expect(LoginPage.ErrorBanner(Page)).ToHaveTextAsync("Invalid username or password.");
        await Expect(Page).ToHaveURLAsync(new Regex("/login$"));
        await Expect(LoginPage.LoginButton(Page)).ToBeEnabledAsync();
    }

    [Test]
    public async Task UnknownUserShowsTheSameError()
    {
        await Page.GotoAsync("/login");
        await LoginPage.SubmitAsync(Page, "no-such-user", "whatever");
        await Expect(LoginPage.ErrorBanner(Page)).ToHaveTextAsync("Invalid username or password.");
    }

    [Test]
    public async Task PressingEnterSubmitsTheForm()
    {
        await Page.GotoAsync("/login");
        await LoginPage.UsernameInput(Page).FillAsync(DemoUsers.Admin.Username);
        await LoginPage.PasswordInput(Page).FillAsync(DemoUsers.Admin.Password);
        await LoginPage.PasswordInput(Page).PressAsync("Enter");
        await Expect(Page).ToHaveURLAsync(new Regex("/home$"));
    }

    [TestCase("//example.com")]
    [TestCase("https://example.com/")]
    public async Task ReturnUrlCannotLeaveTheSite(string returnUrl)
    {
        await Page.GotoAsync($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        await LoginPage.SubmitAsync(Page, DemoUsers.Admin.Username, DemoUsers.Admin.Password);
        await Expect(Page).ToHaveURLAsync($"{Settings.BaseUrl}/home");
    }

    [Test]
    public async Task SignOutEndsTheSession()
    {
        await ApiSession.SignInAsync(Context, DemoUsers.Admin);
        await Page.GotoAsync("/home");
        await Expect(Navigation.SignedInUser(Page)).ToHaveTextAsync("Demo Admin");

        await Navigation.SignOutLink(Page).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/login$"));

        await Page.GotoAsync("/home");
        await Expect(Page).ToHaveURLAsync(new Regex(@"/login\?returnUrl=%2Fhome$"));
        Assert.That((await Context.APIRequest.GetAsync("/api/auth/me")).Status, Is.EqualTo(401));
    }

    [Test]
    public async Task ApiRejectsSignedOutRequests()
    {
        foreach (var url in new[] { "/api/personnel", "/api/training/classes", "/api/medical-surveillance/appointments", "/api/incidents", "/api/access/assignments" })
            Assert.That((await Context.APIRequest.GetAsync(url)).Status, Is.EqualTo(401), url);
    }

    [TestCase("manager", "Demo Manager")]
    [TestCase("viewer", "Demo Viewer")]
    public async Task NavBarShowsTheSignedInUser(string username, string displayName)
    {
        await LoginPage.SignInFromSplashAsync(Page, username, username);
        await Expect(Navigation.SignedInUser(Page)).ToHaveTextAsync(displayName);
    }
}
