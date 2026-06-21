using Microsoft.Playwright;

namespace SafetyOpsTests.Pages;

public static class OMSSPage
{
    public static async Task NavigateToOMSSCreate(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Modules" }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Medical Surveillance (OMSS)" }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Create" }).ClickAsync();
        var frame = page.FrameLocator("#frameCreate");
        await frame.Locator("body").WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    public static async Task NavigateToOMSSEdit(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Modules" }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Medical Surveillance (OMSS)" }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { NameRegex = new System.Text.RegularExpressions.Regex("Edit|Search|Find", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    public static async Task PickTodayFromCalendar(IFrameLocator frame)
    {
        var todayNumber = DateTime.Today.Day.ToString();
        try { await frame.Locator("path").First.ClickAsync(); } catch { }
        var dayLink = frame.GetByRole(AriaRole.Link, new() { Name = todayNumber, Exact = true }).First;
        await dayLink.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await dayLink.ClickAsync();
    }

    public static async Task PickRandomPersonEvaluated(IPage page, IFrameLocator frame)
    {
        var popupTask = page.WaitForPopupAsync();
        await frame.Locator("#txtPerson_HGWselList").ClickAsync();
        var popup = await popupTask;
        await popup.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();
        var personLinks = popup.GetByRole(AriaRole.Link);
        await personLinks.First.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var allLinks = await personLinks.AllAsync();
        if (allLinks.Count == 0) throw new Exception("No person results found in Person Evaluated popup");
        var pick = allLinks[new Random().Next(allLinks.Count)];
        await pick.ClickAsync();
        try { await popup.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 5_000 }); } catch { }
    }

    public static async Task AddRandomWorkTask(IPage page, IFrameLocator frame)
    {
        try { await frame.GetByRole(AriaRole.Link, new() { Name = "Add Work Task(s)" }).ClickAsync(); } catch { }
        var outerFrame = page.Frames.FirstOrDefault(f => f.Name == "frameCreate")
            ?? page.Frames.FirstOrDefault(f => f.Name == "frameEdit")
            ?? page.Frames.FirstOrDefault(f => f.Url.Contains("OMSS"))
            ?? throw new Exception("Could not find OMSS outer frame");
        var nestedIframeHandle = outerFrame.Locator("iframe");
        await nestedIframeHandle.First.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 10_000 });
        var nestedFrame = frame.Locator("iframe").Nth(0).ContentFrame;
        await nestedFrame.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();
        var rows = nestedFrame.GetByRole(AriaRole.Row).Filter(new() { Has = nestedFrame.GetByRole(AriaRole.Checkbox) });
        await rows.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        var allRows = await rows.AllAsync();
        if (allRows.Count == 0) throw new Exception("No work task rows found in Add Work Task dialog");
        var pick = allRows[new Random().Next(allRows.Count)];
        await pick.GetByRole(AriaRole.Checkbox).ClickAsync();
        await nestedFrame.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        try { await nestedIframeHandle.First.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 10_000 }); } catch { }
        await frame.Locator("table").Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex("Stressor ID", System.Text.RegularExpressions.RegexOptions.IgnoreCase) })
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
    }

    public static async Task SelectExamTypesForAllStressors(IPage page, IFrameLocator frame)
    {
        var examTypeSelects = frame.Locator("select[id^=\"ddl\"]");
        await examTypeSelects.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        var allSelects = await examTypeSelects.AllAsync();
        Console.WriteLine($"Found {allSelects.Count} Exam Type dropdown(s)");
        for (int i = 0; i < allSelects.Count; i++)
        {
            var select = allSelects[i];
            var optionValues = await select.Locator("option").EvaluateAllAsync<string[]>(
                "opts => opts.map(o => o.value).filter(v => v !== '' && v !== '0')");
            if (optionValues.Length == 0)
            {
                Console.WriteLine($"Exam Type dropdown #{i + 1} has no selectable options — skipping");
                continue;
            }
            var randomValue = optionValues[new Random().Next(optionValues.Length)];
            await select.SelectOptionAsync(randomValue);
            var alertDialog = page.GetByRole(AriaRole.Alertdialog);
            bool alertVisible;
            try { alertVisible = await alertDialog.IsVisibleAsync(); } catch { alertVisible = false; }
            if (alertVisible)
            {
                var alertText = await alertDialog.TextContentAsync();
                Console.WriteLine($"Exam Type alert on dropdown #{i + 1}: {alertText}");
                var dismissBtn = alertDialog.GetByRole(AriaRole.Button).First;
                if (await dismissBtn.IsVisibleAsync()) await dismissBtn.ClickAsync();
            }
        }
    }
}
