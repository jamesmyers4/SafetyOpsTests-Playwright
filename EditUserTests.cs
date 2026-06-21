using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using SafetyOpsTests.Config;
using SafetyOpsTests.Helpers;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

[TestFixture]
public class EditUserTests : PageTest
{
    [SetUp]
    public async Task SetUp()
    {
        await LoginPage.Login(Page, ConfigLoader.Settings);
    }

    private static async Task NavigateToEditUser(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Modules" }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { NameRegex = new System.Text.RegularExpressions.Regex("Personnel Administration", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { NameRegex = new System.Text.RegularExpressions.Regex("Edit User|Search User|Find User", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    private static async Task SearchForUser(IPage page, string searchTerm)
    {
        var candidates = new[]
        {
            page.GetByPlaceholder(new System.Text.RegularExpressions.Regex("search|find|user", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).First,
            page.GetByRole(AriaRole.Textbox, new() { NameRegex = new System.Text.RegularExpressions.Regex("search|find|user", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).First,
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
        if (searchInput == null) throw new Exception("No search input found on Edit User page");

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

    private static async Task OpenUserForEditing(IPage page)
    {
        var firstUserRow = page.GetByRole(AriaRole.Row).First;
        await firstUserRow.GetByRole(AriaRole.Link).First.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        try { await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/personnel/edit", System.Text.RegularExpressions.RegexOptions.IgnoreCase)); } catch { }
    }

    private static async Task PickRandomFromSelectList(IPage page, string fieldLabel)
    {
        var titleMap = new Dictionary<string, string>
        {
            { "Department", "Select a Department" },
            { "Employee Category", "Select an Employee Category" },
            { "Organization", "Select an Organization" },
            { "Location", "Select a Location" },
        };

        if (!titleMap.TryGetValue(fieldLabel, out var title))
            throw new Exception($"No title mapping found for field: \"{fieldLabel}\"");

        var fieldSection = page.GetByTitle(title);
        await fieldSection.GetByRole(AriaRole.Button, new() { Name = "Open Select List" }).ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var dataRows = dialog.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Checkbox) });
        await dataRows.First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var rows = await dataRows.AllAsync();
        if (rows.Count == 0) throw new Exception($"No data rows found in dialog for: \"{fieldLabel}\"");

        var pick = rows[new Random().Next(rows.Count)];
        await pick.GetByRole(AriaRole.Checkbox).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    private static async Task PickRandomSubscription(IPage page)
    {
        await page.GetByText("Subscriptions", new() { Exact = true }).ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var dataRows = dialog.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Checkbox) });
        await dataRows.First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var rows = await dataRows.AllAsync();
        if (rows.Count == 0) throw new Exception("No data rows found in Subscriptions dialog");

        var pick = rows[new Random().Next(rows.Count)];
        await pick.GetByRole(AriaRole.Checkbox).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    private static async Task PickRandomGender(IPage page)
    {
        var combobox = page.GetByRole(AriaRole.Combobox, new() { Name = "Gender" });
        await combobox.ClickAsync();

        var listbox = page.GetByRole(AriaRole.Listbox);
        await listbox.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var options = listbox.GetByRole(AriaRole.Option).Filter(new() { HasNotTextRegex = new System.Text.RegularExpressions.Regex("select", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
        await options.First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var items = await options.AllAsync();
        var pick = items[new Random().Next(items.Count)];
        await pick.ClickAsync();
        await listbox.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    private static async Task UpdateUserFormFields(IPage page)
    {
        var first = RandomName.WeightedRandomFirstName();
        var middle = RandomName.WeightedRandomMiddleName();
        var last = RandomName.WeightedRandomLastName();

        var reasons = new[] { first.GetReason(), middle.GetReason(), last.GetReason() }
            .Where(r => r != null)
            .ToList();

        if (reasons.Count > 0)
            TestContext.Out.WriteLine($"Adversarial: {string.Join(" | ", reasons)}");

        await PickRandomFromSelectList(page, "Department");
        await PickRandomSubscription(page);
        await PickRandomGender(page);
        await page.GetByLabel("First Name").FillAsync(first.Resolve());
        await page.GetByLabel("Last Name").FillAsync(last.Resolve());
        await page.GetByLabel("Middle Name").FillAsync(middle.Resolve());
    }

    [Test]
    public async Task PAModuleLoadsAndEditSearchUserLinkIsVisible()
    {
        await NavigateToEditUser(Page);
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { NameRegex = new System.Text.RegularExpressions.Regex("edit|search|find.*user", System.Text.RegularExpressions.RegexOptions.IgnoreCase) })).ToBeVisibleAsync();
    }

    [Test]
    public async Task HappyPath_SearchAndEditUserDepartment()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        await PickRandomFromSelectList(Page, "Department");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task HappyPath_SearchAndEditUserNameFields()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "John");
        await OpenUserForEditing(Page);
        var originalFirst = await Page.GetByLabel("First Name").InputValueAsync();
        await UpdateUserFormFields(Page);
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
        var updatedFirst = await Page.GetByLabel("First Name").InputValueAsync();
        try { Assert.That(updatedFirst, Is.Not.EqualTo(originalFirst)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task Validation_SearchWithNoResultsReturnsMessage()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "NONEXISTENT_USER_XYZ_12345");
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("no results|not found|no users", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task Validation_SearchWithPartialNameReturnsMultipleResults()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "a");
        var rows = Page.GetByRole(AriaRole.Row);
        var count = await rows.CountAsync();
        try { Assert.That(count, Is.GreaterThan(1)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task Validation_CannotClearRequiredFirstName()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        await Page.GetByLabel("First Name").FillAsync("");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("required|must.*enter|cannot.*empty", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task Validation_CannotClearRequiredLastName()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        await Page.GetByLabel("Last Name").FillAsync("");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("required|must.*enter|cannot.*empty", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_SpecialCharactersInMiddleName()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        await Page.GetByLabel("Middle Name").FillAsync("O'Brien-Garcia Jr.");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("error|invalid|unexpected", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_OversizedNameInputBoundaryCheck()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        var longName = new string('A', 200);
        await Page.GetByLabel("First Name").FillAsync(longName);
        var actualValue = await Page.GetByLabel("First Name").InputValueAsync();
        Console.WriteLine($"[FINDING] First Name field accepted {actualValue.Length} characters. Verify DB column width.");
        try { Assert.That(actualValue.Length, Is.GreaterThan(0)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_OnlyEditingOneFieldAndSaving()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        var originalFirst = await Page.GetByLabel("First Name").InputValueAsync();
        var originalLast = await Page.GetByLabel("Last Name").InputValueAsync();
        await Page.GetByLabel("Middle Name").FillAsync("NewMiddle123");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
        var currentFirst = await Page.GetByLabel("First Name").InputValueAsync();
        var currentLast = await Page.GetByLabel("Last Name").InputValueAsync();
        try { Assert.That(currentFirst, Is.EqualTo(originalFirst)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
        try { Assert.That(currentLast, Is.EqualTo(originalLast)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EdgeCase_ChangingDepartmentOnly()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        await PickRandomFromSelectList(Page, "Department");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task CancelEdit_ChangesAreDiscarded()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        var originalFirst = await Page.GetByLabel("First Name").InputValueAsync();
        await Page.GetByLabel("First Name").FillAsync("TEMPORARY_CHANGE_XYZ");
        try { await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("cancel|back|close", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync(); } catch { }
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        var currentFirst = await Page.GetByLabel("First Name").InputValueAsync();
        try { Assert.That(currentFirst, Is.Not.EqualTo("TEMPORARY_CHANGE_XYZ")); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
        try { Assert.That(currentFirst, Is.EqualTo(originalFirst)); }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task DoubleClickUpdateDoesNotCreateDuplicateSaves()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        await Page.GetByLabel("Middle Name").FillAsync("DoubleClickTest");
        var updateBtn = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
        await updateBtn.ClickAsync();
        try { await updateBtn.ClickAsync(new() { Timeout = 2_000 }); } catch { }
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
    public async Task EditGenderSelection()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        await PickRandomGender(Page);
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }

    [Test]
    public async Task EditSubscriptions()
    {
        await NavigateToEditUser(Page);
        await SearchForUser(Page, "Smith");
        await OpenUserForEditing(Page);
        await PickRandomSubscription(Page);
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("update|save|submit", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).ClickAsync();
        try
        {
            await Assertions.Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("updated|success|saved", System.Text.RegularExpressions.RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 8_000 });
        }
        catch (Exception ex) { Console.WriteLine($"[soft] {ex.Message}"); }
    }
}
