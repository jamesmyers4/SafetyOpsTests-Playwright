using Microsoft.Playwright;

namespace SafetyOpsTests.Pages;

public static class TrainingAdminPage
{
    public static async Task NavigateToTrainingAdmin(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Modules" }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Training Administration (TA)" }).ClickAsync();
    }

    public static IFrameLocator GetTAFrame(IPage page)
    {
        return page.Locator("iframe").Nth(0).ContentFrame;
    }

    public static async Task SelectCourseViaPopup(IPage page, IFrameLocator frame, string courseName)
    {
        var popupTask = page.WaitForPopupAsync();
        await frame.Locator("#txtCourseTitle_HGWselList").ClickAsync();
        var popup = await popupTask;
        await popup.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await popup.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();
        await popup.GetByRole(AriaRole.Link, new() { Name = courseName }).ClickAsync();
        await Assertions.Expect(frame.Locator("#txtCourseTitle")).Not.ToHaveValueAsync("");
    }

    public static async Task<bool> DismissDuplicateWarningIfPresent(IFrameLocator frame, string buttonName)
    {
        var btn = frame.GetByRole(AriaRole.Button, new() { Name = buttonName });
        bool isVisible;
        try { isVisible = await btn.IsVisibleAsync(); } catch { isVisible = false; }
        if (isVisible)
        {
            await btn.ClickAsync();
            return true;
        }
        return false;
    }
}
