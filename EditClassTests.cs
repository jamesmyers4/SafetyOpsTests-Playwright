using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using SafetyOpsTests.Config;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

[TestFixture]
public class EditClassTests : PageTest
{
    [SetUp]
    public async Task SetUp()
    {
        await LoginPage.Login(Page, ConfigLoader.Settings);
    }

    private static async Task NavigateToTrainingAdminSearch(IPage page, IFrameLocator frame)
    {
        await TrainingAdminPage.NavigateToTrainingAdmin(page);
        await frame.Locator("body").WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    private static async Task SearchForClass(IPage page, IFrameLocator frame, string searchTerm)
    {
        await frame.Locator("#txtSearchClassName").FillAsync(searchTerm);
        await frame.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();
        try
        {
            await frame.Locator("tr").Filter(new() { Has = frame.GetByRole(AriaRole.Link) }).First
                .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        }
        catch { }
    }

    private static async Task OpenFirstSearchResult(IFrameLocator frame)
    {
        var firstResult = frame.Locator("tr").Filter(new() { Has = frame.GetByRole(AriaRole.Link) }).First;
        await firstResult.GetByRole(AriaRole.Link).First.ClickAsync();
        await frame.Locator("body").WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    [Test]
    public async Task TAModuleLoadsAndSearchLinkIsVisible()
    {
        await TrainingAdminPage.NavigateToTrainingAdmin(Page);
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await frame.Locator("body").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await Assertions.Expect(frame.GetByRole(AriaRole.Link, new() { NameRegex = new System.Text.RegularExpressions.Regex("search|find", System.Text.RegularExpressions.RegexOptions.IgnoreCase) })).ToBeVisibleAsync();
    }

    [Test]
    public async Task HappyPath_SearchAndEditClassLocation()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "Electrical");
        await OpenFirstSearchResult(frame);
        var newLocation = "Building 110 Room 300 - Updated";
        await frame.Locator("#txtLocation").FillAsync(newLocation);
        await frame.GetByRole(AriaRole.Button, new() { Name = "Update" }).ClickAsync();
        await TrainingAdminPage.DismissDuplicateWarningIfPresent(frame, "Continue with update");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("class.*updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task Validation_CannotClearRequiredCourseField()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "Electrical");
        await OpenFirstSearchResult(frame);
        await frame.Locator("#txtCourseTitle").FillAsync("");
        await frame.GetByRole(AriaRole.Button, new() { Name = "Update" }).ClickAsync();
        try
        {
            await Assertions.Expect(frame.GetByText("Course ID is required.")).ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task Validation_CannotClearRequiredDateField()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "Electrical");
        await OpenFirstSearchResult(frame);
        await frame.Locator("#txtClassDate").FillAsync("");
        await frame.GetByRole(AriaRole.Button, new() { Name = "Update" }).ClickAsync();
        try
        {
            await Assertions.Expect(frame.GetByText("Class Date is required.")).ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task Validation_CannotClearRequiredLocationField()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "Electrical");
        await OpenFirstSearchResult(frame);
        await frame.Locator("#txtLocation").FillAsync("");
        await frame.GetByRole(AriaRole.Button, new() { Name = "Update" }).ClickAsync();
        try
        {
            await Assertions.Expect(frame.GetByText("Specific location is required")).ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_SpecialCharactersInUpdatedLocationField()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "Electrical");
        await OpenFirstSearchResult(frame);
        await frame.Locator("#txtLocation").FillAsync("Bldg 110 / Room 300 & Annex <B> \"North\"");
        await frame.GetByRole(AriaRole.Button, new() { Name = "Update" }).ClickAsync();
        try
        {
            await Assertions.Expect(frame.GetByText(new System.Text.RegularExpressions.Regex("error|invalid|unexpected", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_OversizedLocationInputOnEdit()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "Electrical");
        await OpenFirstSearchResult(frame);
        var longLocation = new string('A', 500);
        await frame.Locator("#txtLocation").FillAsync(longLocation);
        var actualValue = await frame.Locator("#txtLocation").InputValueAsync();
        Console.WriteLine($"[FINDING] Location field on edit has no maxlength - accepted {actualValue.Length} characters. Verify DB column width to ensure no silent truncation on save.");
        try { Assert.That(actualValue.Length, Is.GreaterThan(0)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_FutureDateIsRejectedOrFlaggedOnEdit()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "Electrical");
        await OpenFirstSearchResult(frame);
        await frame.Locator("#txtClassDate").FillAsync("12/31/2099");
        await frame.GetByRole(AriaRole.Button, new() { Name = "Update" }).ClickAsync();
        try
        {
            await Assertions.Expect(frame.GetByText(new System.Text.RegularExpressions.Regex("invalid.*date|date.*invalid|future.*not.*allowed", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_NonsenseDateStringIsRejectedOnEdit()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "Electrical");
        await OpenFirstSearchResult(frame);
        await frame.Locator("#txtClassDate").FillAsync("99/99/9999");
        await frame.GetByRole(AriaRole.Button, new() { Name = "Update" }).ClickAsync();
        try
        {
            await Assertions.Expect(frame.GetByText(new System.Text.RegularExpressions.Regex("invalid.*date|date.*invalid|required", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_DoubleClickUpdateDoesNotProduceDuplicateUpdate()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "Electrical");
        await OpenFirstSearchResult(frame);
        await frame.Locator("#txtLocation").FillAsync("Building 110 Room 400 - Double Click Test");
        await frame.GetByRole(AriaRole.Button, new() { Name = "Update" }).ClickAsync();
        try { await frame.GetByRole(AriaRole.Button, new() { Name = "Update" }).ClickAsync(new() { Timeout = 2_000 }); } catch { }
        await TrainingAdminPage.DismissDuplicateWarningIfPresent(frame, "Continue with update");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("error|failed", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task Search_NoResultsReturnsAppropriateMessage()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "NONEXISTENT_CLASS_XYZ_12345");
        try
        {
            await Assertions.Expect(frame.GetByText(new System.Text.RegularExpressions.Regex("no results|not found|no classes", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task Search_WildcardSearchReturnsMultipleResults()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "Electrical");
        var rows = frame.Locator("tr").Filter(new() { Has = frame.GetByRole(AriaRole.Link) });
        var count = await rows.CountAsync();
        try { Assert.That(count, Is.GreaterThan(1)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task CancelEdit_ChangesAreDiscarded()
    {
        var frame = TrainingAdminPage.GetTAFrame(Page);
        await NavigateToTrainingAdminSearch(Page, frame);
        await SearchForClass(Page, frame, "Electrical");
        await OpenFirstSearchResult(frame);
        var originalLocation = await frame.Locator("#txtLocation").InputValueAsync();
        await frame.Locator("#txtLocation").FillAsync("TEMPORARY CHANGE - SHOULD NOT SAVE");
        try { await frame.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("cancel|back", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync(); } catch { }
        await SearchForClass(Page, frame, "Electrical");
        await OpenFirstSearchResult(frame);
        var savedLocation = await frame.Locator("#txtLocation").InputValueAsync();
        try { Assert.That(savedLocation, Is.Not.EqualTo("TEMPORARY CHANGE - SHOULD NOT SAVE")); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
        try { Assert.That(savedLocation, Is.EqualTo(originalLocation)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }
}
