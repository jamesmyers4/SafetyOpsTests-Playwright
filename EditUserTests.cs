using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SafetyOpsTests.Helpers;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

/// <summary>
/// Each test edits its own person (created through the API by <see cref="AppPageTest.Data"/>), so tests never
/// change seed data or depend on each other.
/// </summary>
[TestFixture]
public class EditUserTests : AppPageTest
{
    private const string UpdatedMessage = "User updated successfully";

    private Person _person = null!;

    private string Token => Data.Token;

    [SetUp]
    public async Task CreatePersonToEdit() => _person = await Data.PersonAsync($"Edit {Token}", $"Smith {Token}");

    private async Task OpenPersonForEditing()
    {
        await PersonnelPage.OpenEditSearchAsync(Page);
        await PersonnelPage.SearchUsersAsync(Page, Token);
        await PersonnelPage.OpenOnlyResultAsync(Page);
    }

    private async Task ExpectUpdated()
    {
        await Expect(PersonnelPage.Status(Page, UpdatedMessage)).ToBeVisibleAsync();
        await Expect(PersonnelPage.Alert(Page)).ToHaveCountAsync(0);
    }

    private Task<Person> Saved() => AppApi.GetPersonAsync(Api, _person.Id);

    [Test]
    public async Task PersonnelModuleLoadsAndEditSearchUserLinkIsVisible()
    {
        await PersonnelPage.OpenAsync(Page);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Edit/Search User" })).ToBeVisibleAsync();
        await PersonnelPage.OpenEditSearchAsync(Page);
    }

    [Test]
    public async Task HappyPath_SearchAndEditUserDepartment()
    {
        await OpenPersonForEditing();
        var department = await PersonnelPage.PickRandomFromSelectListAsync(Page, "Department");
        await PersonnelPage.UpdateButton(Page).ClickAsync();
        await ExpectUpdated();
        Assert.That((await Saved()).Department, Is.EqualTo(department));
    }

    [Test]
    public async Task HappyPath_SearchAndEditUserNameFields()
    {
        await OpenPersonForEditing();
        var first = RandomName.WeightedRandomFirstName();
        var middle = RandomName.WeightedRandomMiddleName();
        var last = RandomName.WeightedRandomLastName();
        var reasons = new[] { first.GetReason(), middle.GetReason(), last.GetReason() }.Where(r => r != null).ToList();
        if (reasons.Count > 0)
            TestContext.Out.WriteLine($"Adversarial: {string.Join(" | ", reasons)}");

        await PersonnelPage.PickRandomFromSelectListAsync(Page, "Department");
        await PersonnelPage.PickRandomFromSelectListAsync(Page, "Subscriptions");
        await PersonnelPage.PickRandomGenderAsync(Page);
        await Page.GetByLabel("First Name").FillAsync(first.Resolve());
        await Page.GetByLabel("Last Name").FillAsync(last.Resolve());
        await Page.GetByLabel("Middle Name").FillAsync(middle.Resolve());
        await PersonnelPage.UpdateButton(Page).ClickAsync();

        // The expected outcome follows from the input: the form requires first and last name,
        // and the server caps names at 100 characters.
        if (string.IsNullOrWhiteSpace(first.Resolve()) || string.IsNullOrWhiteSpace(last.Resolve()))
        {
            await Expect(PersonnelPage.Alert(Page)).ToContainTextAsync("is required");
            Assert.That((await Saved()).FirstName, Is.EqualTo(_person.FirstName));
        }
        else if (new[] { first, middle, last }.Any(n => n.Resolve().Length > TestData.MaxNameLength))
        {
            await Expect(PersonnelPage.Alert(Page)).ToContainTextAsync("maximum length of 100");
            Assert.That((await Saved()).FirstName, Is.EqualTo(_person.FirstName));
        }
        else
        {
            await ExpectUpdated();
            var saved = await Saved();
            Assert.Multiple(() =>
            {
                Assert.That(saved.FirstName, Is.EqualTo(first.Resolve()));
                Assert.That(saved.LastName, Is.EqualTo(last.Resolve()));
                Assert.That(saved.MiddleName, Is.EqualTo(middle.Resolve()));
            });
        }
    }

    [Test]
    public async Task Validation_SearchWithNoResultsReturnsMessage()
    {
        await PersonnelPage.OpenEditSearchAsync(Page);
        await PersonnelPage.SearchUsersAsync(Page, "NONEXISTENT_USER_XYZ_12345");
        await Expect(Page.GetByText("No results found for your search.")).ToBeVisibleAsync();
        await Expect(PersonnelPage.SearchResultRows(Page)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Validation_SearchWithPartialNameReturnsMultipleResults()
    {
        await PersonnelPage.OpenEditSearchAsync(Page);
        await PersonnelPage.SearchUsersAsync(Page, "a");
        Assert.That(await PersonnelPage.SearchResultRows(Page).CountAsync(), Is.GreaterThan(1));
    }

    [Test]
    public async Task Validation_CannotClearRequiredFirstName()
    {
        await OpenPersonForEditing();
        await Page.GetByLabel("First Name").FillAsync("");
        await PersonnelPage.UpdateButton(Page).ClickAsync();
        await Expect(PersonnelPage.Alert(Page)).ToHaveTextAsync("First Name is required");
        Assert.That((await Saved()).FirstName, Is.EqualTo(_person.FirstName));
    }

    [Test]
    public async Task Validation_CannotClearRequiredLastName()
    {
        await OpenPersonForEditing();
        await Page.GetByLabel("Last Name").FillAsync("");
        await PersonnelPage.UpdateButton(Page).ClickAsync();
        await Expect(PersonnelPage.Alert(Page)).ToHaveTextAsync("Last Name is required");
        Assert.That((await Saved()).LastName, Is.EqualTo(_person.LastName));
    }

    [Test]
    public async Task EdgeCase_SpecialCharactersInMiddleName()
    {
        await OpenPersonForEditing();
        await Page.GetByLabel("Middle Name").FillAsync("O'Brien-Garcia Jr.");
        await PersonnelPage.UpdateButton(Page).ClickAsync();
        await ExpectUpdated();
        Assert.That((await Saved()).MiddleName, Is.EqualTo("O'Brien-Garcia Jr."));
    }

    [Test]
    public async Task EdgeCase_OversizedNameInputBoundaryCheck()
    {
        await OpenPersonForEditing();
        var longName = new string('A', 200);
        await Page.GetByLabel("First Name").FillAsync(longName);
        // The input has no maxlength, so the server is the only guard.
        await Expect(Page.GetByLabel("First Name")).ToHaveValueAsync(longName);
        await PersonnelPage.UpdateButton(Page).ClickAsync();
        await Expect(PersonnelPage.Alert(Page)).ToContainTextAsync("maximum length of 100");
        Assert.That((await Saved()).FirstName, Is.EqualTo(_person.FirstName));
    }

    [Test]
    public async Task EdgeCase_OnlyEditingOneFieldAndSaving()
    {
        await OpenPersonForEditing();
        await Page.GetByLabel("Middle Name").FillAsync("NewMiddle123");
        await PersonnelPage.UpdateButton(Page).ClickAsync();
        await ExpectUpdated();
        var saved = await Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.MiddleName, Is.EqualTo("NewMiddle123"));
            Assert.That(saved with { MiddleName = _person.MiddleName }, Is.EqualTo(_person));
        });
    }

    [Test]
    public async Task EdgeCase_ChangingDepartmentOnly()
    {
        await OpenPersonForEditing();
        var department = await PersonnelPage.PickRandomFromSelectListAsync(Page, "Department");
        await PersonnelPage.UpdateButton(Page).ClickAsync();
        await ExpectUpdated();
        var saved = await Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.Department, Is.EqualTo(department));
            Assert.That(saved with { Department = _person.Department }, Is.EqualTo(_person));
        });
    }

    [Test]
    public async Task CancelEdit_ChangesAreDiscarded()
    {
        await OpenPersonForEditing();
        await Page.GetByLabel("First Name").FillAsync("TEMPORARY_CHANGE_XYZ");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/personnel/edit$"));

        await PersonnelPage.SearchUsersAsync(Page, Token);
        await PersonnelPage.OpenOnlyResultAsync(Page);
        await Expect(Page.GetByLabel("First Name")).ToHaveValueAsync(_person.FirstName);
    }

    [Test]
    public async Task DoubleClickUpdateDoesNotCreateDuplicateSaves()
    {
        await OpenPersonForEditing();
        await Page.GetByLabel("Middle Name").FillAsync("DoubleClickTest");
        await PersonnelPage.UpdateButton(Page).DblClickAsync();
        await ExpectUpdated();
        var matches = await AppApi.SearchPeopleAsync(Api, Token);
        Assert.That(matches.Select(p => p.Id), Is.EqualTo(new[] { _person.Id }));
        Assert.That(matches[0].MiddleName, Is.EqualTo("DoubleClickTest"));
    }

    [Test]
    public async Task EditGenderSelection()
    {
        await OpenPersonForEditing();
        var gender = await PersonnelPage.PickRandomGenderAsync(Page);
        await PersonnelPage.UpdateButton(Page).ClickAsync();
        await ExpectUpdated();
        Assert.That((await Saved()).Gender, Is.EqualTo(gender));

        await Page.ReloadAsync();
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Gender" })).ToHaveTextAsync(gender);
    }

    [Test]
    public async Task EditSubscriptions()
    {
        await OpenPersonForEditing();
        var subscription = await PersonnelPage.PickRandomFromSelectListAsync(Page, "Subscriptions");
        await PersonnelPage.UpdateButton(Page).ClickAsync();
        await ExpectUpdated();
        Assert.That((await Saved()).Subscription, Is.EqualTo(subscription));
    }
}
