using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SafetyOpsTests.Pages;

/// <summary>
/// Medical Surveillance: the create page (form in #create-frame), the search/edit page (form in
/// #edit-frame), the person picker popup, and the work task picker nested inside the form's frame.
/// </summary>
public static class MedicalSurveillancePage
{
    public const string SearchHeading = "Search / Edit Medical Surveillance Records";
    public const string UpdatedMessage = "Record updated successfully";

    private static readonly Random Random = new();

    public static IFrameLocator CreateFrame(IPage page) => page.FrameLocator("#create-frame");
    public static IFrameLocator EditFrame(IPage page) => page.FrameLocator("#edit-frame");

    public static ILocator AppointmentDate(IFrameLocator frame) => frame.Locator("#appointment-date");
    public static ILocator PersonEvaluated(IFrameLocator frame) => frame.Locator("#person-evaluated");
    public static ILocator ExamTypeSelects(IFrameLocator frame) => frame.Locator("select[id^='exam-type-']");
    public static ILocator StressorRows(IFrameLocator frame) =>
        frame.Locator("table").Filter(new() { HasText = "Stressor ID" }).Locator("tbody tr");
    public static ILocator FrameAlert(IFrameLocator frame) => frame.GetByRole(AriaRole.Alert);
    public static ILocator Button(IFrameLocator frame, string name) => frame.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
    public static ILocator SearchResultRows(IPage page) => page.Locator("table tbody tr");

    public static async Task OpenAsync(IPage page)
    {
        await Navigation.OpenModuleAsync(page, Navigation.MedicalSurveillance);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Medical Surveillance", Exact = true })).ToBeVisibleAsync();
    }

    public static async Task<IFrameLocator> OpenCreateAsync(IPage page)
    {
        await OpenAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Create", Exact = true }).ClickAsync();
        var frame = CreateFrame(page);
        await Assertions.Expect(frame.GetByRole(AriaRole.Heading, new() { Name = "Create Medical Surveillance Record" })).ToBeVisibleAsync();
        return frame;
    }

    public static async Task OpenSearchAsync(IPage page)
    {
        await OpenAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Edit / Search" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = SearchHeading })).ToBeVisibleAsync();
    }

    public static async Task SearchAsync(IPage page, string term)
    {
        await page.GetByLabel("Search medical surveillance records").FillAsync(term);
        await page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Loading" })).ToHaveCountAsync(0);
    }

    /// <summary>Opens the only search result in the edit frame and waits for it to load.</summary>
    public static async Task<IFrameLocator> OpenOnlyResultAsync(IPage page)
    {
        await Assertions.Expect(SearchResultRows(page)).ToHaveCountAsync(1);
        await SearchResultRows(page).GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
        var frame = EditFrame(page);
        await Assertions.Expect(frame.GetByRole(AriaRole.Heading, new() { Name = "Edit Medical Surveillance Record" })).ToBeVisibleAsync();
        await Assertions.Expect(PersonEvaluated(frame)).Not.ToHaveValueAsync("");
        return frame;
    }

    /// <summary>Picks today from the calendar icon next to Appointment Date.</summary>
    public static async Task PickTodayFromCalendarAsync(IFrameLocator frame)
    {
        await frame.Locator("svg:has(path)").First.ClickAsync();
        await frame.GetByRole(AriaRole.Link, new() { Name = DateTime.Today.Day.ToString(), Exact = true }).ClickAsync();
        await Assertions.Expect(AppointmentDate(frame)).ToHaveValueAsync(DateTime.Today.ToString("MM/dd/yyyy"));
    }

    /// <summary>
    /// Picks a random person in the person picker popup (it posts the choice back and closes).
    /// Returns the chosen name. <paramref name="except"/> excludes a name, to force a change.
    /// </summary>
    public static async Task<string> PickRandomPersonEvaluatedAsync(IPage page, IFrameLocator frame, string? except = null)
    {
        var popup = await page.RunAndWaitForPopupAsync(() => frame.Locator("#person-picker-button").ClickAsync());
        await popup.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();
        var links = popup.Locator("table tbody tr a");
        await Assertions.Expect(links.First).ToBeVisibleAsync();
        var names = (await links.AllInnerTextsAsync()).Select(n => n.Trim()).Where(n => n != except).ToList();
        Assert.That(names, Is.Not.Empty, "No people in the person picker");
        var name = names[Random.Next(names.Count)];
        await popup.GetByRole(AriaRole.Link, new() { Name = name, Exact = true }).First.ClickAsync();
        await Assertions.Expect(PersonEvaluated(frame)).ToHaveValueAsync(name);
        return name;
    }

    /// <summary>Adds one random work task through the nested picker frame; returns the task's name.</summary>
    public static async Task<string> AddRandomWorkTaskAsync(IFrameLocator frame)
    {
        await frame.GetByRole(AriaRole.Link, new() { Name = "Add Work Task(s)" }).ClickAsync();
        var picker = frame.FrameLocator("iframe[title='Work Task Picker']");
        await picker.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();
        var rows = picker.Locator("tbody tr");
        await Assertions.Expect(rows.First).ToBeVisibleAsync();
        var pick = rows.Nth(Random.Next(await rows.CountAsync()));
        var task = (await pick.Locator("td").Nth(1).InnerTextAsync()).Trim();
        await pick.GetByRole(AriaRole.Checkbox).CheckAsync();
        await picker.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(frame.Locator("iframe[title='Work Task Picker']")).ToHaveCountAsync(0);
        await Assertions.Expect(StressorRows(frame).First).ToBeVisibleAsync();
        return task;
    }

    /// <summary>Sets every Exam Type select to a random exam type; returns stressor id → exam type.</summary>
    public static async Task<Dictionary<string, string>> SelectExamTypesForAllStressorsAsync(IFrameLocator frame)
    {
        var chosen = new Dictionary<string, string>();
        var rows = StressorRows(frame);
        var count = await rows.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "No stressors to set exam types for");
        for (var i = 0; i < count; i++)
        {
            var row = rows.Nth(i);
            var stressorId = (await row.Locator("td").First.InnerTextAsync()).Trim();
            var select = row.Locator("select[id^='exam-type-']");
            var options = await select.Locator("option").EvaluateAllAsync<string[]>("opts => opts.map(o => o.value).filter(v => v !== '')");
            var examType = options[Random.Next(options.Length)];
            await select.SelectOptionAsync(examType);
            chosen[stressorId] = examType;
        }
        return chosen;
    }

    public static int AppointmentIdFromUrl(string url) =>
        int.Parse(Regex.Match(url, @"/medical-surveillance/appointments/(\d+)").Groups[1].Value);
}
