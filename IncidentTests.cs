using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SafetyOpsTests.Helpers;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

/// <summary>
/// Incident Reports: create, search/filter, edit, delete. Every incident a test creates has the
/// test's unique token in its location, so the base fixture's cleanup finds and deletes it.
/// </summary>
[TestFixture]
public class IncidentTests : AppPageTest
{
    private string Token => Data.Token;

    private Task<Incident> Arrange(string status = "Open", string category = "NearMiss") =>
        Data.IncidentAsync($"Loading Dock {Token}-{status}-{category}", status, category);

    [Test]
    public async Task ListShowsSeededIncidentsAndPager()
    {
        await IncidentsPage.OpenAsync(Page);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Create Incident" })).ToBeVisibleAsync();
        await Expect(IncidentsPage.Rows(Page).Filter(new() { HasText = "Building 300 Paint Shop" })).ToHaveCountAsync(1);
        await Expect(IncidentsPage.PagerText(Page)).ToBeVisibleAsync();
    }

    [Test]
    public async Task CreateIncidentWithAllFields()
    {
        await IncidentsPage.OpenCreateAsync(Page);
        var occurredAt = DateTime.Today.AddDays(-2).AddHours(14).AddMinutes(15);
        var location = $"Building 500 Warehouse Aisle 3 {Token}";

        await Page.Locator("#incident-occurred-at").FillAsync(occurredAt.ToString("yyyy-MM-ddTHH:mm"));
        await AccessLevelsPage.SelectOrgUnitAsync(Page.Locator("#incident-org-unit"), OrgUnits.EastWarehouse);
        await Page.Locator("#incident-location").FillAsync(location);
        await Page.Locator("#incident-category").SelectOptionAsync(new SelectOptionValue { Label = "Property Damage" });
        await Page.Locator("#incident-severity").SelectOptionAsync(new SelectOptionValue { Label = "High" });
        await Page.Locator("#incident-reported-by").SelectOptionAsync(new SelectOptionValue { Label = "John Johnson" });
        await Page.Locator("#incident-description").FillAsync("Pallet jack struck a racking upright; rack inspected and tagged out.");
        await Expect(Page.Locator("#incident-status")).ToHaveCountAsync(0); // status is only editable later
        await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Report" }).ClickAsync();

        await Page.WaitForURLAsync(new Regex(@"/incidents/\d+$"));
        var id = IncidentsPage.IncidentIdFromUrl(Page.Url);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = $"Incident Report #{id}" })).ToBeVisibleAsync();
        await Expect(IncidentsPage.Detail(Page, "Status", "Open")).ToBeVisibleAsync();
        await Expect(IncidentsPage.Detail(Page, "Occurred", occurredAt.ToString("MM/dd/yyyy HH:mm"))).ToBeVisibleAsync();
        await Expect(IncidentsPage.Detail(Page, "Category", "Property Damage")).ToBeVisibleAsync();
        await Expect(IncidentsPage.Detail(Page, "Org Unit", OrgUnits.EastWarehouse)).ToBeVisibleAsync();

        var saved = await AppApi.GetIncidentAsync(Api, id);
        Assert.Multiple(() =>
        {
            Assert.That(saved.Location, Is.EqualTo(location));
            Assert.That(saved.OccurredAt, Does.StartWith(occurredAt.ToString("yyyy-MM-ddTHH:mm")));
            Assert.That(saved.Category, Is.EqualTo("PropertyDamage"));
            Assert.That(saved.Severity, Is.EqualTo("High"));
            Assert.That(saved.ReportedByName, Is.EqualTo("John Johnson"));
            Assert.That(saved.Status, Is.EqualTo("Open"));
            Assert.That(saved.OrgUnitName, Is.EqualTo(OrgUnits.EastWarehouse));
        });
    }

    [Test]
    public async Task CreateRequiresLocationDescriptionAndReporter()
    {
        await IncidentsPage.OpenCreateAsync(Page);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Report" }).ClickAsync();
        await Expect(IncidentsPage.Alert(Page).Locator("div"))
            .ToHaveTextAsync(["Location is required.", "Description is required.", "Reported by is required."]);
        await Expect(Page).ToHaveURLAsync(new Regex("/incidents/new$"));
    }

    [Test]
    public async Task FutureOccurredAtIsRejectedByTheServer()
    {
        await IncidentsPage.OpenCreateAsync(Page);
        await Page.Locator("#incident-occurred-at").FillAsync(DateTime.Today.AddDays(2).AddHours(9).ToString("yyyy-MM-ddTHH:mm"));
        await Page.Locator("#incident-location").FillAsync($"Future {Token}");
        await Page.Locator("#incident-reported-by").SelectOptionAsync(new SelectOptionValue { Label = "John Smith" });
        await Page.Locator("#incident-description").FillAsync("Should not save.");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Report" }).ClickAsync();

        await Expect(IncidentsPage.Alert(Page)).ToHaveTextAsync("The incident can't be in the future.");
        Assert.That(await AppApi.SearchIncidentsAsync(Api, Token), Is.Empty);
    }

    [Test]
    public async Task SearchFindsIncidentsByLocation()
    {
        await Arrange("Open");
        await Arrange("Closed");
        await IncidentsPage.OpenAsync(Page);
        await IncidentsPage.SearchAsync(Page, Token);
        await Expect(IncidentsPage.Rows(Page)).ToHaveCountAsync(2);
        await Expect(IncidentsPage.PagerText(Page)).ToHaveTextAsync("Page 1 of 1 (2 incidents)");
    }

    [Test]
    public async Task SearchWithNoMatchesSaysSo()
    {
        await IncidentsPage.OpenAsync(Page);
        await IncidentsPage.SearchAsync(Page, $"NONEXISTENT {Token}");
        await Expect(Page.GetByText("No incidents match.")).ToBeVisibleAsync();
        await Expect(IncidentsPage.Rows(Page)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task FilterByStatus()
    {
        await Arrange("Open");
        var closed = await Arrange("Closed");
        await IncidentsPage.OpenAsync(Page);
        await IncidentsPage.SearchAsync(Page, Token);
        await Expect(IncidentsPage.Rows(Page)).ToHaveCountAsync(2);

        await IncidentsPage.FilterByStatusAsync(Page, "Closed");
        await Expect(IncidentsPage.Rows(Page)).ToHaveCountAsync(1);
        await Expect(IncidentsPage.Rows(Page).First).ToContainTextAsync(closed.Location);
        await Expect(IncidentsPage.PagerText(Page)).ToHaveTextAsync("Page 1 of 1 (1 incident)");

        await IncidentsPage.FilterByStatusAsync(Page, "All statuses");
        await Expect(IncidentsPage.Rows(Page)).ToHaveCountAsync(2);
    }

    [Test]
    public async Task FilterByCategoryCombinesWithStatus()
    {
        var fire = await Arrange("Open", "Fire");
        await Arrange("Open", "Injury");
        await Arrange("Closed", "Fire");
        await IncidentsPage.OpenAsync(Page);
        await IncidentsPage.SearchAsync(Page, Token);
        await Expect(IncidentsPage.Rows(Page)).ToHaveCountAsync(3);

        await IncidentsPage.FilterByCategoryAsync(Page, "Fire");
        await Expect(IncidentsPage.Rows(Page)).ToHaveCountAsync(2);
        await IncidentsPage.FilterByStatusAsync(Page, "Open");
        await Expect(IncidentsPage.Rows(Page)).ToHaveCountAsync(1);
        await Expect(IncidentsPage.Rows(Page).First).ToContainTextAsync(fire.Location);
    }

    [Test]
    public async Task EditIncidentStatusAndSeverity()
    {
        var incident = await Arrange();
        await Page.GotoAsync($"/incidents/{incident.Id}");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
        await Expect(Page.Locator("#incident-location")).ToHaveValueAsync(incident.Location);
        await Page.Locator("#incident-status").SelectOptionAsync(new SelectOptionValue { Label = "Under Review" });
        await Page.Locator("#incident-severity").SelectOptionAsync(new SelectOptionValue { Label = "Critical" });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Incident updated successfully" })).ToBeVisibleAsync();
        await Expect(IncidentsPage.Detail(Page, "Status", "Under Review")).ToBeVisibleAsync();
        await Expect(IncidentsPage.Detail(Page, "Severity", "Critical")).ToBeVisibleAsync();
        Assert.That(await AppApi.GetIncidentAsync(Api, incident.Id),
            Is.EqualTo(incident with { Status = "UnderReview", Severity = "Critical" }));
    }

    [Test]
    public async Task EditRejectsClearedRequiredFields()
    {
        var incident = await Arrange();
        await Page.GotoAsync($"/incidents/{incident.Id}");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
        await Page.Locator("#incident-location").FillAsync("");
        await Page.Locator("#incident-description").FillAsync("");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" }).ClickAsync();

        await Expect(IncidentsPage.Alert(Page).Locator("div")).ToHaveTextAsync(["Location is required.", "Description is required."]);
        Assert.That(await AppApi.GetIncidentAsync(Api, incident.Id), Is.EqualTo(incident));
    }

    [Test]
    public async Task CancelEditKeepsTheIncident()
    {
        var incident = await Arrange();
        await Page.GotoAsync($"/incidents/{incident.Id}");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
        await Page.Locator("#incident-location").FillAsync("TEMPORARY CHANGE");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();

        await Expect(IncidentsPage.Detail(Page, "Location", incident.Location)).ToBeVisibleAsync();
        Assert.That(await AppApi.GetIncidentAsync(Api, incident.Id), Is.EqualTo(incident));
    }

    [Test]
    public async Task DeleteIncidentAfterConfirmation()
    {
        var incident = await Arrange();
        await Page.GotoAsync($"/incidents/{incident.Id}");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Expect(Page.GetByText("Delete this incident?")).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/incidents$"));
        Assert.That((await Api.GetAsync($"/api/incidents/{incident.Id}")).Status, Is.EqualTo(404));
    }

    [Test]
    public async Task ViewerCanReadButNotEditNorthPlantIncidents()
    {
        var incident = await Data.IncidentAsync($"North Dock {Token}", orgUnitId: 4);
        var logistics = await Data.IncidentAsync($"East Dock {Token}", orgUnitId: 6);
        await SwitchUserAsync(DemoUsers.Viewer);

        await Page.GotoAsync("/incidents");
        await IncidentsPage.SearchAsync(Page, Token);
        await Expect(IncidentsPage.Rows(Page)).ToHaveCountAsync(1);
        await Expect(IncidentsPage.Rows(Page).First).ToContainTextAsync(incident.Location);

        await Page.GotoAsync($"/incidents/{incident.Id}");
        await Expect(IncidentsPage.Detail(Page, "Location", incident.Location)).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Edit" })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Delete" })).ToHaveCountAsync(0);

        Assert.That((await Context.APIRequest.GetAsync($"/api/incidents/{logistics.Id}")).Status, Is.EqualTo(404));
    }
}
