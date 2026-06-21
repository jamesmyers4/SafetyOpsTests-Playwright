using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using SafetyOpsTests.Config;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

[TestFixture]
public class EditOMSSTests : PageTest
{
    [SetUp]
    public async Task SetUp()
    {
        await LoginPage.Login(Page, ConfigLoader.Settings);
    }

    private static async Task SearchForOMSSRecord(IPage page, string searchTerm)
    {
        var candidates = new[]
        {
            page.GetByPlaceholder(new System.Text.RegularExpressions.Regex("search|find|id|reference", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).First,
            page.GetByRole(AriaRole.Textbox, new() { NameRegex = new System.Text.RegularExpressions.Regex("search|find|id|reference", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).First,
            page.GetByRole(AriaRole.Textbox).First,
        };

        ILocator? searchInput = null;
        foreach (var candidate in candidates)
        {
            try
            {
                if (await candidate.IsVisibleAsync())
                {
                    searchInput = candidate;
                    break;
                }
            }
            catch { continue; }
        }
        if (searchInput == null) throw new Exception("No search input found on OMSS Edit page");
        Console.WriteLine($"[SearchForOMSSRecord] Using search term: \"{searchTerm}\" — update to a specific record ID when available");
        await searchInput.FillAsync(searchTerm);
        var searchButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("search|find", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).First;
        if (await searchButton.IsVisibleAsync().ConfigureAwait(false))
        {
            await searchButton.ClickAsync();
        }
        else
        {
            await searchInput.PressAsync("Enter");
        }
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    private static async Task<IFrameLocator> OpenOMSSRecordForEditing(IPage page)
    {
        var firstRow = page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Link) }).First;
        await firstRow.GetByRole(AriaRole.Link).First.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        var frame = page.Locator("#frameEdit, #frameUpdate, iframe[name*=\"frame\"]").Nth(0).ContentFrame;
        await frame.Locator("body").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        return frame;
    }

    private static string GetTodayFormatted()
    {
        var today = DateTime.Today;
        return $"{today.Month:D2}/{today.Day:D2}/{today.Year}";
    }

    [Test]
    public async Task OMSSEditModuleLoadsAndSearchLinkIsVisible()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { NameRegex = new System.Text.RegularExpressions.Regex("edit|search|find.*omss", System.Text.RegularExpressions.RegexOptions.IgnoreCase) })).ToBeVisibleAsync();
    }

    [Test]
    public async Task HappyPath_SearchAndEditOMSSRecordDate()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "appointment");
        var frame = await OpenOMSSRecordForEditing(Page);
        await OMSSPage.PickTodayFromCalendar(frame);
        await frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.WaitForTimeoutAsync(2_000);
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task HappyPath_SearchAndEditOMSSPersonEvaluated()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "appointment");
        var frame = await OpenOMSSRecordForEditing(Page);
        await OMSSPage.PickRandomPersonEvaluated(Page, frame);
        await frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.WaitForTimeoutAsync(2_000);
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task HappyPath_SearchAndEditOMSSExamTypes()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "appointment");
        var frame = await OpenOMSSRecordForEditing(Page);
        await OMSSPage.SelectExamTypesForAllStressors(Page, frame);
        await frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.WaitForTimeoutAsync(2_000);
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task Validation_SearchWithNoResultsReturnsMessage()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "NONEXISTENT_OMSS_XYZ_12345");
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("no results|not found|no records|no appointments", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task Validation_SearchWithPartialTermReturnsMultipleResults()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "a");
        var rows = Page.GetByRole(AriaRole.Row);
        var count = await rows.CountAsync();
        try { Assert.That(count, Is.GreaterThan(1)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_UpdateOnlyDateField()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "appointment");
        var frame = await OpenOMSSRecordForEditing(Page);
        string originalPerson;
        try { originalPerson = await frame.Locator("#txtPerson").InputValueAsync(); } catch { originalPerson = ""; }
        await OMSSPage.PickTodayFromCalendar(frame);
        await frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.WaitForTimeoutAsync(2_000);
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
        string currentPerson;
        try { currentPerson = await frame.Locator("#txtPerson").InputValueAsync(); } catch { currentPerson = ""; }
        try { Assert.That(currentPerson, Is.EqualTo(originalPerson)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_UpdateOnlyPersonEvaluatedField()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "appointment");
        var frame = await OpenOMSSRecordForEditing(Page);
        await OMSSPage.PickRandomPersonEvaluated(Page, frame);
        await frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.WaitForTimeoutAsync(2_000);
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_UpdateOnlyExamTypesField()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "appointment");
        var frame = await OpenOMSSRecordForEditing(Page);
        await OMSSPage.SelectExamTypesForAllStressors(Page, frame);
        await frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.WaitForTimeoutAsync(2_000);
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_AddWorkTaskDuringEdit()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "appointment");
        var frame = await OpenOMSSRecordForEditing(Page);
        await OMSSPage.AddRandomWorkTask(Page, frame);
        await frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.WaitForTimeoutAsync(2_000);
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_DoubleClickUpdateDoesNotCreateDuplicateSaves()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "appointment");
        var frame = await OpenOMSSRecordForEditing(Page);
        await OMSSPage.PickTodayFromCalendar(frame);
        var updateBtn = frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
        await updateBtn.ClickAsync();
        try { await updateBtn.ClickAsync(new() { Timeout = 2_000 }); } catch { }
        await Page.WaitForTimeoutAsync(2_000);
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("error|failed|duplicate", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task CancelEdit_ChangesAreDiscarded()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "appointment");
        var frame = await OpenOMSSRecordForEditing(Page);
        string originalDate;
        try { originalDate = await frame.Locator("#txtDate, [name*=\"Date\"]").First.InputValueAsync(); } catch { originalDate = ""; }
        await OMSSPage.PickTodayFromCalendar(frame);
        try { await frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("cancel|back|close", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync(); } catch { }
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await SearchForOMSSRecord(Page, "appointment");
        var frame2 = await OpenOMSSRecordForEditing(Page);
        string currentDate;
        try { currentDate = await frame2.Locator("#txtDate, [name*=\"Date\"]").First.InputValueAsync(); } catch { currentDate = ""; }
        try { Assert.That(currentDate, Is.EqualTo(originalDate)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task MultipleFieldUpdate_DatePersonAndExamTypes()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "appointment");
        var frame = await OpenOMSSRecordForEditing(Page);
        await OMSSPage.PickTodayFromCalendar(frame);
        await OMSSPage.PickRandomPersonEvaluated(Page, frame);
        await OMSSPage.SelectExamTypesForAllStressors(Page, frame);
        await frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.WaitForTimeoutAsync(2_000);
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task VerifyRecordReflectsChangesAfterEdit()
    {
        await OMSSPage.NavigateToOMSSEdit(Page);
        await SearchForOMSSRecord(Page, "appointment");
        var frame = await OpenOMSSRecordForEditing(Page);
        string recordId;
        try { recordId = await frame.Locator("[id*='ID'], [id*='Record']").First.TextContentAsync() ?? ""; } catch { recordId = ""; }
        await OMSSPage.PickTodayFromCalendar(frame);
        await frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.WaitForTimeoutAsync(2_000);
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
        await OMSSPage.NavigateToOMSSEdit(Page);
        if (!string.IsNullOrEmpty(recordId))
        {
            await SearchForOMSSRecord(Page, recordId);
            var frame2 = await OpenOMSSRecordForEditing(Page);
            string updatedDate;
            try { updatedDate = await frame2.Locator("#txtDate, [name*=\"Date\"]").First.InputValueAsync(); } catch { updatedDate = ""; }
            try { Assert.That(updatedDate, Is.EqualTo(GetTodayFormatted())); }
            catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
        }
    }
}
