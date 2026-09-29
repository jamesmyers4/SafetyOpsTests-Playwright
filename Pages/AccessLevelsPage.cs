using Microsoft.Playwright;

namespace SafetyOpsTests.Pages;

/// <summary>Personnel → Access Levels: the org tree and, for admins, role grants and revokes.</summary>
public static class AccessLevelsPage
{
    public const string AdminsOnlyMessage = "Only administrators can view and manage role assignments.";

    public static ILocator Tree(IPage page) => page.GetByRole(AriaRole.Tree, new() { Name = "Organization units" });

    /// <summary>Codes of the org units in the tree, in document order.</summary>
    public static Task<string[]> TreeCodesAsync(IPage page) =>
        Tree(page).Locator("li[data-org-unit]").EvaluateAllAsync<string[]>("els => els.map(e => e.dataset.orgUnit)");

    public static ILocator GrantButton(IPage page) => page.GetByRole(AriaRole.Button, new() { Name = "Grant Role" });

    public static ILocator AssignmentRow(IPage page, string userName, string orgUnitName, string role) =>
        page.Locator("table tbody tr")
            .Filter(new() { HasText = $"({userName})" })
            .Filter(new() { HasText = orgUnitName })
            .Filter(new() { HasText = role });

    public static async Task OpenAsync(IPage page)
    {
        await PersonnelPage.OpenAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Access Levels" }).ClickAsync();
        await page.WaitForURLAsync("**/personnel/access-levels");
        await Assertions.Expect(Tree(page)).ToBeVisibleAsync();
    }

    public static async Task GrantAsync(IPage page, string userOption, string orgUnitName, string role)
    {
        await page.Locator("#grant-user").SelectOptionAsync(new SelectOptionValue { Label = userOption });
        await SelectOrgUnitAsync(page.Locator("#grant-org-unit"), orgUnitName);
        await page.Locator("#grant-role").SelectOptionAsync(role);
        await GrantButton(page).ClickAsync();
    }

    /// <summary>Selects an org unit by name in an indented OrgUnitSelect.</summary>
    public static async Task SelectOrgUnitAsync(ILocator select, string orgUnitName)
    {
        var value = await select.EvaluateAsync<string>(
            "(s, name) => [...s.options].find(o => o.text.trim() === name)?.value ?? ''", orgUnitName);
        Assert.That(value, Is.Not.Empty, $"No \"{orgUnitName}\" option");
        await select.SelectOptionAsync(value);
    }

    /// <summary>Option texts of an OrgUnitSelect, without the indentation.</summary>
    public static async Task<string[]> OrgUnitOptionsAsync(ILocator select) =>
        (await select.Locator("option").AllInnerTextsAsync()).Select(t => t.Trim()).ToArray();
}
