using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SafetyOpsTests.Pages;

/// <summary>Incident Reports: the filterable list, the report form, and the detail page.</summary>
public static class IncidentsPage
{
    public static ILocator Rows(IPage page) => page.Locator("table tbody tr");
    public static ILocator PagerText(IPage page) => page.GetByText(new Regex(@"^Page \d+ of \d+"));
    public static ILocator Alert(IPage page) => page.GetByRole(AriaRole.Alert);

    public static async Task OpenAsync(IPage page)
    {
        await Navigation.OpenModuleAsync(page, Navigation.IncidentReports);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Incident Reports", Exact = true })).ToBeVisibleAsync();
    }

    public static async Task OpenCreateAsync(IPage page)
    {
        await OpenAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Create Incident" }).ClickAsync();
        await page.WaitForURLAsync("**/incidents/new");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Report an Incident" })).ToBeVisibleAsync();
    }

    /// <summary>Searches the list and waits for it to show the pager (or "No incidents match.").</summary>
    public static async Task SearchAsync(IPage page, string term)
    {
        await page.GetByLabel("Search incidents").FillAsync(term);
        await page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
    }

    public static Task FilterByStatusAsync(IPage page, string label) =>
        page.GetByLabel("Filter by status").SelectOptionAsync(new SelectOptionValue { Label = label });

    public static Task FilterByCategoryAsync(IPage page, string label) =>
        page.GetByLabel("Filter by category").SelectOptionAsync(new SelectOptionValue { Label = label });

    /// <summary>A detail-page field such as "Status: Open".</summary>
    public static ILocator Detail(IPage page, string label, string value) =>
        page.Locator("p").Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(label)}: {Regex.Escape(value)}$") });

    public static int IncidentIdFromUrl(string url) => int.Parse(Regex.Match(url, @"/incidents/(\d+)").Groups[1].Value);
}
