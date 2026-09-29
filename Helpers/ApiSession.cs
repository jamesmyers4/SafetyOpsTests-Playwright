using Microsoft.Playwright;

namespace SafetyOpsTests.Helpers;

/// <summary>
/// Signs in through the API. For a browser context, the auth cookie lands in the context's cookie
/// jar, so pages, iframes, and popups in it are all signed in without going through the UI.
/// </summary>
public static class ApiSession
{
    public static Task SignInAsync(IBrowserContext context, Credentials user) => SignInAsync(context.APIRequest, user);

    public static async Task SignInAsync(IAPIRequestContext api, Credentials user)
    {
        var response = await api.PostAsync("/api/auth/login", new()
        {
            DataObject = new { username = user.Username, password = user.Password },
        });
        if (!response.Ok)
            throw new InvalidOperationException($"API sign-in as '{user.Username}' failed: {response.Status} {await response.TextAsync()}");
    }
}
