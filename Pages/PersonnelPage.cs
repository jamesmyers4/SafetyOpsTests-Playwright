using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SafetyOpsTests.Pages;

/// <summary>Personnel home, Add User, Edit/Search User, and Edit User pages.</summary>
public static class PersonnelPage
{
    private static readonly Random Random = new();

    private static readonly Dictionary<string, (string Button, string DialogTitle)> SelectLists = new()
    {
        ["Department"] = ("Open Select List", "Select a Department"),
        ["Subscriptions"] = ("Subscriptions", "Subscriptions"),
        ["Employee Category"] = ("Open Select List", "Select an Employee Category"),
    };

    public static ILocator Heading(IPage page, string name) =>
        page.GetByRole(AriaRole.Heading, new() { Name = name, Exact = true });

    public static ILocator Alert(IPage page) => page.GetByRole(AriaRole.Alert);

    public static ILocator Status(IPage page, string text) =>
        page.GetByRole(AriaRole.Status).Filter(new() { HasText = text });

    public static ILocator UpdateButton(IPage page) => page.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true });

    public static async Task OpenAsync(IPage page)
    {
        await Navigation.OpenModuleAsync(page, Navigation.Personnel);
        await Assertions.Expect(Heading(page, "Personnel")).ToBeVisibleAsync();
    }

    public static async Task OpenAddUserAsync(IPage page)
    {
        await OpenAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Add New User" }).ClickAsync();
        await page.WaitForURLAsync("**/personnel/create");
        await Assertions.Expect(Heading(page, "Add New User")).ToBeVisibleAsync();
    }

    public static async Task OpenEditSearchAsync(IPage page)
    {
        await OpenAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Edit/Search User" }).ClickAsync();
        await page.WaitForURLAsync("**/personnel/edit");
        await Assertions.Expect(Heading(page, "Edit / Search User")).ToBeVisibleAsync();
    }

    /// <summary>Searches on the Edit/Search User page and waits for the search to finish.</summary>
    public static async Task SearchUsersAsync(IPage page, string term)
    {
        await page.GetByLabel("Search users").FillAsync(term);
        await page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Loading" })).ToHaveCountAsync(0);
    }

    /// <summary>Rows in the Edit/Search User results table (header excluded).</summary>
    public static ILocator SearchResultRows(IPage page) => page.Locator("table tbody tr");

    /// <summary>Opens the only search result for editing.</summary>
    public static async Task OpenOnlyResultAsync(IPage page)
    {
        await Assertions.Expect(SearchResultRows(page)).ToHaveCountAsync(1);
        await SearchResultRows(page).GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
        await Assertions.Expect(Heading(page, "Edit User")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("First Name")).Not.ToHaveValueAsync("");
    }

    /// <summary>Picks a random option in one of the checkbox select dialogs and returns it.</summary>
    public static async Task<string> PickRandomFromSelectListAsync(IPage page, string field)
    {
        var (button, title) = SelectLists[field];
        var fieldRow = page.Locator("label", new() { HasTextRegex = new Regex($"^{Regex.Escape(field)}$") }).Locator("..");
        await fieldRow.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = title });
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        var rows = dialog.Locator("tbody tr");
        var count = await rows.CountAsync();
        Assert.That(count, Is.GreaterThan(0), $"No options in the \"{title}\" dialog");

        var pick = rows.Nth(Random.Next(count));
        var value = (await pick.Locator("td").Nth(1).InnerTextAsync()).Trim();
        await pick.GetByRole(AriaRole.Checkbox).CheckAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(dialog).ToBeHiddenAsync();
        await Assertions.Expect(fieldRow).ToContainTextAsync(value);
        return value;
    }

    /// <summary>Add User's Gender is a native select.</summary>
    public static async Task<string> SelectRandomGenderAsync(IPage page)
    {
        var select = page.Locator("select[aria-label='Gender']");
        var options = await select.Locator("option").EvaluateAllAsync<string[]>("opts => opts.map(o => o.value).filter(v => v !== '')");
        var pick = options[Random.Next(options.Length)];
        await select.SelectOptionAsync(pick);
        return pick;
    }

    /// <summary>Edit User's Gender is a custom combobox with a listbox.</summary>
    public static async Task<string> PickRandomGenderAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Combobox, new() { Name = "Gender" }).ClickAsync();
        var listbox = page.GetByRole(AriaRole.Listbox, new() { Name = "Gender" });
        await Assertions.Expect(listbox).ToBeVisibleAsync();
        var options = listbox.GetByRole(AriaRole.Option);
        var pick = options.Nth(Random.Next(await options.CountAsync()));
        var value = (await pick.InnerTextAsync()).Trim();
        await pick.ClickAsync();
        await Assertions.Expect(listbox).ToBeHiddenAsync();
        return value;
    }

    /// <summary>Picks a random org unit in the Organization Unit select and returns its name.</summary>
    public static async Task<string> SelectRandomOrgUnitAsync(IPage page)
    {
        var select = page.Locator("#org-unit");
        var options = await select.Locator("option").AllInnerTextsAsync();
        var index = Random.Next(options.Count);
        await select.SelectOptionAsync(new SelectOptionValue { Index = index });
        return options[index].Trim();
    }
}
