using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SafetyOpsTests.Pages;

/// <summary>The splash page ("/") and the sign-in form ("/login").</summary>
public static class LoginPage
{
    public static ILocator UsernameInput(IPage page) => page.Locator("#username");
    public static ILocator PasswordInput(IPage page) => page.Locator("#password");
    public static ILocator LoginButton(IPage page) => page.GetByRole(AriaRole.Button, new() { Name = "Login" });
    public static ILocator ErrorBanner(IPage page) => page.GetByRole(AriaRole.Alert);

    /// <summary>Signs in through the UI from the splash page and waits for the home page.</summary>
    public static async Task SignInFromSplashAsync(IPage page, string username, string password)
    {
        await page.GotoAsync("/");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await page.WaitForURLAsync("**/login**");
        await SubmitAsync(page, username, password);
        await page.WaitForURLAsync(new Regex("/home$"));
    }

    /// <summary>Fills and submits the sign-in form on the current page without waiting for a result.</summary>
    public static async Task SubmitAsync(IPage page, string username, string password)
    {
        await UsernameInput(page).FillAsync(username);
        await PasswordInput(page).FillAsync(password);
        await LoginButton(page).ClickAsync();
    }
}
