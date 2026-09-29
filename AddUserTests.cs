using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SafetyOpsTests.Helpers;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

[TestFixture]
public class AddUserTests : AppPageTest
{
    [Test]
    public async Task FillsAndSubmitsAddNewUserForm()
    {
        await PersonnelPage.OpenAddUserAsync(Page);

        var token = Data.Token;
        var first = RandomName.WeightedRandomFirstName();
        var middle = RandomName.WeightedRandomMiddleName();
        var last = RandomName.WeightedRandomLastName();
        var firstName = $"{first.Resolve()} {token}";
        var middleName = $"{middle.Resolve()} {token}";
        var lastName = $"{last.Resolve()} {token}";

        var reasons = new[] { first.GetReason(), middle.GetReason(), last.GetReason() }.Where(r => r != null).ToList();
        if (reasons.Count > 0)
            TestContext.Out.WriteLine($"Adversarial: {string.Join(" | ", reasons)}");

        var orgUnit = await PersonnelPage.SelectRandomOrgUnitAsync(Page);
        var department = await PersonnelPage.PickRandomFromSelectListAsync(Page, "Department");
        await PersonnelPage.PickRandomFromSelectListAsync(Page, "Subscriptions");
        await PersonnelPage.SelectRandomGenderAsync(Page);
        await PersonnelPage.PickRandomFromSelectListAsync(Page, "Employee Category");
        await Page.GetByLabel("First Name").FillAsync(firstName);
        await Page.GetByLabel("Last Name").FillAsync(lastName);
        await Page.GetByLabel("Middle Name").FillAsync(middleName);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Generate Random Number" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add User" }).ClickAsync();

        if (new[] { firstName, middleName, lastName }.Any(n => n.Length > TestData.MaxNameLength))
        {
            // The server caps names at 100 characters; the form shows its validation message.
            await Expect(PersonnelPage.Alert(Page)).ToContainTextAsync("maximum length of 100");
            await Expect(Page).ToHaveURLAsync(new Regex("/personnel/create$"));
            return;
        }

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "User Successfully Added" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add Another User" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Return to Personnel" })).ToBeVisibleAsync();

        var saved = await AppApi.SearchPeopleAsync(Api, token);
        Assert.That(saved, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(saved[0].FirstName, Is.EqualTo(firstName));
            Assert.That(saved[0].LastName, Is.EqualTo(lastName));
            Assert.That(saved[0].Department, Is.EqualTo(department));
            Assert.That(saved[0].OrgUnitName, Is.EqualTo(orgUnit));
            Assert.That(saved[0].EmployeeNumber, Does.Match("^[0-9]{7}$"));
        });
    }

    [Test]
    public async Task AddUserRequiresFirstAndLastName()
    {
        await PersonnelPage.OpenAddUserAsync(Page);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add User" }).ClickAsync();
        await Expect(PersonnelPage.Alert(Page)).ToContainTextAsync("The FirstName field is required.");
        await Expect(PersonnelPage.Alert(Page)).ToContainTextAsync("The LastName field is required.");
        await Expect(Page).ToHaveURLAsync(new Regex("/personnel/create$"));
    }

    [Test]
    public async Task DeleteFromPersonnelListAsksForConfirmation()
    {
        var token = Data.Token;
        await Data.PersonAsync("Delete", $"Me {token}");

        await PersonnelPage.OpenAsync(Page);
        await Page.GetByLabel("Filter users").FillAsync(token);
        await Page.GetByLabel("Filter users").PressAsync("Enter");
        var row = Page.Locator("table tbody tr").Filter(new() { HasText = token });
        await Expect(row).ToHaveCountAsync(1);

        string? dialogMessage = null;
        Page.Dialog += async (_, dialog) => { dialogMessage = dialog.Message; await dialog.AcceptAsync(); };
        await row.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();

        await Expect(row).ToHaveCountAsync(0);
        Assert.That(dialogMessage, Is.EqualTo($"Delete Delete Me {token}?"));
        Assert.That(await AppApi.SearchPeopleAsync(Api, token), Is.Empty);
    }

    [Test]
    public async Task DismissingDeleteConfirmationKeepsThePerson()
    {
        var token = Data.Token;
        await Data.PersonAsync("Keep", $"Me {token}");

        await PersonnelPage.OpenAsync(Page);
        await Page.GetByLabel("Filter users").FillAsync(token);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
        var row = Page.Locator("table tbody tr").Filter(new() { HasText = token });
        await Expect(row).ToHaveCountAsync(1);

        Page.Dialog += async (_, dialog) => await dialog.DismissAsync();
        await row.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();

        await Expect(row).ToHaveCountAsync(1);
        Assert.That(await AppApi.SearchPeopleAsync(Api, token), Has.Count.EqualTo(1));
    }
}
