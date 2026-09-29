using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SafetyOpsTests.Pages;

/// <summary>
/// The Training shell page and the create/search/edit class forms it hosts in an iframe. A frame
/// posts a validated draft to the shell (postMessage); the shell's Save button writes it.
/// </summary>
public static class TrainingPage
{
    public const string ElectricalLowVoltage = "Electrical - Low Voltage";

    public static IFrameLocator Frame(IPage page) => page.FrameLocator("iframe[title='Training Frame']");

    public static ILocator ShellSaveButton(IPage page) => page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true });
    public static ILocator ShellAlert(IPage page) => page.GetByRole(AriaRole.Alert);
    public static ILocator ShellStatus(IPage page) => page.GetByRole(AriaRole.Status);
    public static ILocator FrameAlert(IFrameLocator frame) => frame.GetByRole(AriaRole.Alert);
    public static ILocator Button(IFrameLocator frame, string name) => frame.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    public static ILocator ClassDate(IFrameLocator frame) => frame.Locator("#class-date");
    public static ILocator Location(IFrameLocator frame) => frame.Locator("#class-location");
    public static ILocator CourseTitle(IFrameLocator frame) => frame.Locator("#course-title");

    public static async Task OpenAsync(IPage page)
    {
        await Navigation.OpenModuleAsync(page, Navigation.Training);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Training", Exact = true })).ToBeVisibleAsync();
    }

    /// <summary>Opens Training; the frame starts on the class search.</summary>
    public static async Task<IFrameLocator> OpenSearchAsync(IPage page)
    {
        await OpenAsync(page);
        var frame = Frame(page);
        await Assertions.Expect(frame.GetByRole(AriaRole.Link, new() { Name = "Find / Search Classes" })).ToBeVisibleAsync();
        return frame;
    }

    public static async Task<IFrameLocator> OpenCreateClassAsync(IPage page)
    {
        await OpenAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Create Class" }).ClickAsync();
        var frame = Frame(page);
        await Assertions.Expect(frame.GetByRole(AriaRole.Heading, new() { Name = "Create Training Class" })).ToBeVisibleAsync();
        return frame;
    }

    /// <summary>Opens the frame's calendar and picks today.</summary>
    public static async Task PickTodayAsync(IFrameLocator frame)
    {
        await ClassDate(frame).ClickAsync();
        await frame.GetByRole(AriaRole.Link, new() { Name = DateTime.Today.Day.ToString(), Exact = true }).ClickAsync();
        await Assertions.Expect(ClassDate(frame)).ToHaveValueAsync(DateTime.Today.ToString("MM/dd/yyyy"));
    }

    /// <summary>Picks a course in the course picker popup, which posts it back to the frame and closes.</summary>
    public static async Task SelectCourseViaPopupAsync(IPage page, IFrameLocator frame, string courseName)
    {
        var popup = await page.RunAndWaitForPopupAsync(() => frame.Locator("#course-picker-button").ClickAsync());
        await popup.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();
        await Popups.ClickAndExpectCloseAsync(popup, popup.GetByRole(AriaRole.Link, new() { Name = courseName, Exact = true }));
        await Assertions.Expect(CourseTitle(frame)).ToHaveValueAsync(courseName);
    }

    /// <summary>Today, the given location, and <see cref="ElectricalLowVoltage"/>.</summary>
    public static async Task FillValidClassAsync(IPage page, IFrameLocator frame, string location)
    {
        await PickTodayAsync(frame);
        await Location(frame).FillAsync(location);
        await SelectCourseViaPopupAsync(page, frame, ElectricalLowVoltage);
    }

    /// <summary>
    /// After Create/Update, waits until the frame has either handed the draft to the shell (Save
    /// appears) or shown the duplicate warning. Returns true if it was the duplicate warning.
    /// </summary>
    public static async Task<bool> WaitForSaveOrDuplicateAsync(IPage page, IFrameLocator frame, string continueButton)
    {
        var duplicate = Button(frame, continueButton);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (await duplicate.IsVisibleAsync()) return true;
            if (await ShellSaveButton(page).IsVisibleAsync()) return false;
            await Task.Delay(100);
        }
        throw new TimeoutException("Neither the shell's Save button nor the duplicate warning appeared.");
    }

    /// <summary>Continues past the duplicate warning if it appears, then waits for the shell's Save button.</summary>
    public static async Task<bool> DismissDuplicateWarningIfPresentAsync(IPage page, IFrameLocator frame, string continueButton)
    {
        var wasDuplicate = await WaitForSaveOrDuplicateAsync(page, frame, continueButton);
        if (wasDuplicate) await Button(frame, continueButton).ClickAsync();
        await Assertions.Expect(ShellSaveButton(page)).ToBeVisibleAsync();
        return wasDuplicate;
    }

    /// <summary>Clicks the shell's Save for a new class and returns the created class id from the detail page URL.</summary>
    public static async Task<int> SaveNewClassAsync(IPage page)
    {
        await ShellSaveButton(page).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/training/classes/\d+$"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Training Class Details" })).ToBeVisibleAsync();
        return ClassIdFromUrl(page.Url);
    }

    public static int ClassIdFromUrl(string url) => int.Parse(Regex.Match(url, @"/training/classes/(\d+)").Groups[1].Value);

    public static ILocator SearchResultRows(IFrameLocator frame) => frame.Locator("table tbody tr");

    public static async Task SearchClassesAsync(IFrameLocator frame, string term)
    {
        await frame.Locator("#class-search").FillAsync(term);
        await Button(frame, "Search").ClickAsync();
        await Assertions.Expect(frame.GetByRole(AriaRole.Status).Filter(new() { HasText = "Loading" })).ToHaveCountAsync(0);
    }

    /// <summary>Opens the only search result in the edit form.</summary>
    public static async Task OpenOnlyResultAsync(IFrameLocator frame)
    {
        await Assertions.Expect(SearchResultRows(frame)).ToHaveCountAsync(1);
        await SearchResultRows(frame).GetByRole(AriaRole.Link).ClickAsync();
        await Assertions.Expect(frame.GetByRole(AriaRole.Heading, new() { Name = "Edit Training Class" })).ToBeVisibleAsync();
    }
}
