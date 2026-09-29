using Microsoft.Playwright;

namespace SafetyOpsTests.Pages;

/// <summary>Helpers for the app's picker popups, which post their choice to the opener and close.</summary>
public static class Popups
{
    /// <summary>
    /// Clicks something that makes <paramref name="popup"/> close itself. The popup can close before
    /// Playwright finishes the click, which surfaces as a "target closed" PlaywrightException; that is
    /// the expected outcome, so it is only rethrown if the popup is still open. Callers assert the
    /// result in the opener.
    /// </summary>
    public static async Task ClickAndExpectCloseAsync(IPage popup, ILocator target)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        popup.Close += (_, _) => closed.TrySetResult();
        if (popup.IsClosed) closed.TrySetResult();

        try
        {
            await target.ClickAsync();
        }
        catch (PlaywrightException) when (popup.IsClosed)
        {
        }
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
