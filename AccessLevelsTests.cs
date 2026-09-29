using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SafetyOpsTests.Helpers;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

/// <summary>
/// Org-scoped roles: what each demo user can see and do, and granting/revoking roles as Admin.
/// Seeded roles: admin = Admin on the whole org, manager = Manager on Manufacturing Division,
/// viewer = Viewer on North Plant.
/// </summary>
[TestFixture]
public class AccessLevelsTests : AppPageTest
{
    [Test]
    public async Task AdminSeesTheWholeOrgTreeAndRoleAssignments()
    {
        await AccessLevelsPage.OpenAsync(Page);
        Assert.That(await AccessLevelsPage.TreeCodesAsync(Page), Is.EqualTo(OrgUnits.AllCodes));
        await Expect(AccessLevelsPage.GrantButton(Page)).ToBeVisibleAsync();
        await Expect(AccessLevelsPage.AssignmentRow(Page, "manager", OrgUnits.Manufacturing, "Manager")).ToHaveCountAsync(1);
        await Expect(AccessLevelsPage.AssignmentRow(Page, "viewer", OrgUnits.NorthPlant, "Viewer")).ToHaveCountAsync(1);
        // Admins can't revoke their own role.
        await Expect(AccessLevelsPage.AssignmentRow(Page, "admin", OrgUnits.Organization, "Admin")).ToContainTextAsync("(you)");
        await Expect(AccessLevelsPage.AssignmentRow(Page, "admin", OrgUnits.Organization, "Admin").GetByRole(AriaRole.Button)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ViewerSeesOnlyNorthPlantAndNoRoleManagement()
    {
        await SwitchUserAsync(DemoUsers.Viewer);
        await Page.GotoAsync("/home");
        await AccessLevelsPage.OpenAsync(Page);
        Assert.That(await AccessLevelsPage.TreeCodesAsync(Page), Is.EqualTo(new[] { "MFG-N" }));
        await Expect(Page.GetByText(AccessLevelsPage.AdminsOnlyMessage)).ToBeVisibleAsync();
        await Expect(AccessLevelsPage.GrantButton(Page)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ViewerSeesOnlyNorthPlantPeopleAndNoWriteActions()
    {
        await SwitchUserAsync(DemoUsers.Viewer);
        await Page.GotoAsync("/home");
        await PersonnelPage.OpenAsync(Page);

        var orgUnitColumn = Page.Locator("table tbody tr td:nth-child(4)");
        await Expect(orgUnitColumn.First).ToBeVisibleAsync();
        Assert.That((await orgUnitColumn.AllInnerTextsAsync()).Distinct(), Is.EqualTo(new[] { OrgUnits.NorthPlant }));
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Add New User" })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Delete" })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ViewerEditUserPageIsReadOnly()
    {
        await SwitchUserAsync(DemoUsers.Viewer);
        await Page.GotoAsync("/home");
        await PersonnelPage.OpenEditSearchAsync(Page);
        await PersonnelPage.SearchUsersAsync(Page, "Jane");
        await PersonnelPage.OpenOnlyResultAsync(Page);

        await Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = "You have read-only access to this record." })).ToBeVisibleAsync();
        await Expect(PersonnelPage.UpdateButton(Page)).ToBeDisabledAsync();
        await Expect(Page.Locator("#org-unit")).ToBeDisabledAsync();
    }

    [Test]
    public async Task ViewerSeesNoCreateActionsInAnyModule()
    {
        await SwitchUserAsync(DemoUsers.Viewer);
        await Page.GotoAsync("/home");

        await TrainingPage.OpenAsync(Page);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Create Class" })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Search / Edit Classes" })).ToBeVisibleAsync();

        await MedicalSurveillancePage.OpenAsync(Page);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Create", Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Edit / Search" })).ToBeVisibleAsync();

        await IncidentsPage.OpenAsync(Page);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Create Incident" })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ViewerWritesAreRejectedByTheApi()
    {
        await SwitchUserAsync(DemoUsers.Viewer);
        var response = await Context.APIRequest.PostAsync("/api/personnel", new()
        {
            DataObject = new { firstName = "Not", lastName = "Allowed" },
        });
        Assert.That(response.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task ManagerOrgUnitPickerIsTheManufacturingSubtree()
    {
        await SwitchUserAsync(DemoUsers.Manager);
        await Page.GotoAsync("/home");
        await AccessLevelsPage.OpenAsync(Page);
        Assert.That(await AccessLevelsPage.TreeCodesAsync(Page), Is.EqualTo(new[] { "MFG", "MFG-N", "MFG-S" }));
        await Expect(Page.GetByText(AccessLevelsPage.AdminsOnlyMessage)).ToBeVisibleAsync();

        await PersonnelPage.OpenAddUserAsync(Page);
        Assert.That(await AccessLevelsPage.OrgUnitOptionsAsync(Page.Locator("#org-unit")), Is.EqualTo(OrgUnits.ManufacturingSubtree));

        await PersonnelPage.OpenAsync(Page);
        var orgUnitColumn = Page.Locator("table tbody tr td:nth-child(4)");
        await Expect(orgUnitColumn.First).ToBeVisibleAsync();
        Assert.That((await orgUnitColumn.AllInnerTextsAsync()).Distinct(), Is.EquivalentTo(new[] { OrgUnits.NorthPlant, OrgUnits.SouthPlant }));
    }

    [Test]
    public async Task AdminGrantIsVisibleToTheGranteeImmediatelyThenRevoke()
    {
        // Whatever happens below, the viewer must not keep the East Warehouse role.
        Data.OnCleanUp(async api =>
        {
            foreach (var a in await AppApi.ListRoleAssignmentsAsync(api))
                if (a.UserName == "viewer" && a.OrgUnitName == OrgUnits.EastWarehouse)
                    await AppApi.RevokeRoleAsync(api, a.Id);
        });

        var viewerPage = await OpenPageAsAsync(DemoUsers.Viewer);
        await viewerPage.GotoAsync("/personnel/access-levels");
        await Expect(AccessLevelsPage.Tree(viewerPage)).ToBeVisibleAsync();
        Assert.That(await AccessLevelsPage.TreeCodesAsync(viewerPage), Is.EqualTo(new[] { "MFG-N" }));

        await AccessLevelsPage.OpenAsync(Page);
        await AccessLevelsPage.GrantAsync(Page, "Demo Viewer (viewer)", OrgUnits.EastWarehouse, "Viewer");
        await Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Granted Viewer on East Warehouse to Demo Viewer." })).ToBeVisibleAsync();
        var row = AccessLevelsPage.AssignmentRow(Page, "viewer", OrgUnits.EastWarehouse, "Viewer");
        await Expect(row).ToHaveCountAsync(1);

        // The viewer's existing session picks up the new role on its next page load.
        await viewerPage.ReloadAsync();
        await Expect(AccessLevelsPage.Tree(viewerPage).Locator("li[data-org-unit='LOG-E']")).ToBeVisibleAsync();
        Assert.That(await AccessLevelsPage.TreeCodesAsync(viewerPage), Is.EqualTo(new[] { "MFG-N", "LOG-E" }));
        await viewerPage.GotoAsync("/personnel");
        await Expect(viewerPage.Locator("table tbody tr").Filter(new() { HasText = "John Johnson" })).ToHaveCountAsync(1);

        string? confirmMessage = null;
        Page.Dialog += async (_, dialog) => { confirmMessage = dialog.Message; await dialog.AcceptAsync(); };
        await row.GetByRole(AriaRole.Button, new() { Name = "Revoke" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Revoked Viewer on East Warehouse from Demo Viewer." })).ToBeVisibleAsync();
        await Expect(row).ToHaveCountAsync(0);
        Assert.That(confirmMessage, Is.EqualTo("Revoke Viewer on East Warehouse from Demo Viewer?"));

        await viewerPage.GotoAsync("/personnel/access-levels");
        await Expect(AccessLevelsPage.Tree(viewerPage)).ToBeVisibleAsync();
        Assert.That(await AccessLevelsPage.TreeCodesAsync(viewerPage), Is.EqualTo(new[] { "MFG-N" }));
    }

    [Test]
    public async Task GrantWithoutAUserShowsAnError()
    {
        await AccessLevelsPage.OpenAsync(Page);
        await AccessLevelsPage.GrantButton(Page).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Choose a user and an organization unit.");
    }

    [Test]
    public async Task ViewerCannotReachRoleAssignmentsThroughTheApi()
    {
        await SwitchUserAsync(DemoUsers.Viewer);
        Assert.That((await Context.APIRequest.GetAsync("/api/access/assignments")).Status, Is.EqualTo(403));
        await Page.GotoAsync("/personnel/access-levels");
        await Expect(Page).ToHaveURLAsync(new Regex("/personnel/access-levels$"));
        await Expect(Page.GetByText(AccessLevelsPage.AdminsOnlyMessage)).ToBeVisibleAsync();
    }
}
