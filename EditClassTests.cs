using Microsoft.Playwright;
using SafetyOpsTests.Helpers;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

/// <summary>
/// Each test edits its own class (created through the API with a unique location and an old
/// date that no other class shares), so edits never touch seed data.
/// </summary>
[TestFixture]
public class EditClassTests : AppPageTest
{
    private const string SavedMessage = "Class saved successfully";

    private TrainingClass _class = null!;

    private string Token => Data.Token;

    [SetUp]
    public async Task CreateClassToEdit()
    {
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(-Random.Shared.Next(40, 3000));
        _class = await Data.ClassAsync($"Building 110 Room 300 - {Token}", date);
    }

    private async Task<IFrameLocator> OpenClassForEditing()
    {
        var frame = await TrainingPage.OpenSearchAsync(Page);
        await TrainingPage.SearchClassesAsync(frame, Token);
        await TrainingPage.OpenOnlyResultAsync(frame);
        await Expect(TrainingPage.Location(frame)).ToHaveValueAsync(_class.Location);
        return frame;
    }

    /// <summary>Update in the frame, continue past a duplicate warning if any, then Save in the shell.</summary>
    private async Task UpdateAndSave(IFrameLocator frame)
    {
        await TrainingPage.Button(frame, "Update").ClickAsync();
        await TrainingPage.DismissDuplicateWarningIfPresentAsync(Page, frame, "Continue with update");
        await TrainingPage.ShellSaveButton(Page).ClickAsync();
    }

    private async Task ExpectSaved()
    {
        await Expect(TrainingPage.ShellStatus(Page).Filter(new() { HasText = SavedMessage })).ToBeVisibleAsync();
        await Expect(TrainingPage.ShellAlert(Page)).ToHaveCountAsync(0);
    }

    private async Task ExpectFrameRejects(IFrameLocator frame, string message)
    {
        await TrainingPage.Button(frame, "Update").ClickAsync();
        await Expect(TrainingPage.FrameAlert(frame)).ToHaveTextAsync(message);
        await Expect(TrainingPage.ShellSaveButton(Page)).ToBeHiddenAsync();
        Assert.That(await Saved(), Is.EqualTo(_class));
    }

    private Task<TrainingClass> Saved() => AppApi.GetClassAsync(Api, _class.Id);

    [Test]
    public async Task TrainingModuleLoadsAndSearchLinkIsVisible()
    {
        var frame = await TrainingPage.OpenSearchAsync(Page);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Search / Edit Classes" })).ToBeVisibleAsync();
        await Expect(frame.Locator("#class-search")).ToBeVisibleAsync();
    }

    [Test]
    public async Task HappyPath_SearchAndEditClassLocation()
    {
        var frame = await OpenClassForEditing();
        var newLocation = $"Building 110 Room 300 - Updated {Token}";
        await TrainingPage.Location(frame).FillAsync(newLocation);
        await UpdateAndSave(frame);
        await ExpectSaved();
        Assert.That(await Saved(), Is.EqualTo(_class with { Location = newLocation }));
    }

    [Test]
    public async Task Validation_CannotClearRequiredCourseField()
    {
        var frame = await OpenClassForEditing();
        await TrainingPage.CourseTitle(frame).FillAsync("");
        await TrainingPage.Button(frame, "Update").ClickAsync();
        await TrainingPage.DismissDuplicateWarningIfPresentAsync(Page, frame, "Continue with update");
        await TrainingPage.ShellSaveButton(Page).ClickAsync();
        await Expect(TrainingPage.ShellStatus(Page)).ToBeVisibleAsync();

        // Clearing the title box doesn't clear the course: the class keeps its course.
        // (No "Course ID is required." message is shown — see finding A-03.)
        var saved = await Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.CourseId, Is.EqualTo(_class.CourseId));
            Assert.That(saved.CourseTitle, Is.EqualTo(_class.CourseTitle));
        });
    }

    [Test]
    public async Task Validation_CannotClearRequiredDateField()
    {
        var frame = await OpenClassForEditing();
        await TrainingPage.ClassDate(frame).FillAsync("");
        await ExpectFrameRejects(frame, "Class Date is required.");
    }

    [Test]
    public async Task Validation_CannotClearRequiredLocationField()
    {
        var frame = await OpenClassForEditing();
        await TrainingPage.Location(frame).FillAsync("");
        await ExpectFrameRejects(frame, "Specific location is required");
    }

    [Test]
    public async Task EdgeCase_SpecialCharactersInUpdatedLocationField()
    {
        var frame = await OpenClassForEditing();
        var location = $"Bldg 110 / Room 300 & Annex <B> \"North\" {Token}";
        await TrainingPage.Location(frame).FillAsync(location);
        await UpdateAndSave(frame);
        await ExpectSaved();
        Assert.That((await Saved()).Location, Is.EqualTo(location));
    }

    [Test]
    public async Task EdgeCase_OversizedLocationInputOnEdit()
    {
        var frame = await OpenClassForEditing();
        var longLocation = Token + new string('A', 500 - Token.Length);
        await TrainingPage.Location(frame).FillAsync(longLocation);
        // The field has no maxlength; the API is the guard (200 characters).
        await Expect(TrainingPage.Location(frame)).ToHaveValueAsync(longLocation);
        await UpdateAndSave(frame);

        await Expect(TrainingPage.ShellAlert(Page)).ToHaveTextAsync("The field Location must be a string with a maximum length of 200.");
        Assert.That(await Saved(), Is.EqualTo(_class));
    }

    [Test]
    public async Task EdgeCase_FutureDateIsRejectedOrFlaggedOnEdit()
    {
        var frame = await OpenClassForEditing();
        await TrainingPage.ClassDate(frame).FillAsync("12/31/2099");
        await ExpectFrameRejects(frame, "Future dates are not allowed.");
    }

    [Test]
    public async Task EdgeCase_NonsenseDateStringIsRejectedOnEdit()
    {
        var frame = await OpenClassForEditing();
        await TrainingPage.ClassDate(frame).FillAsync("99/99/9999");
        await ExpectFrameRejects(frame, "Invalid date. Please enter a valid date.");
    }

    [Test]
    public async Task EdgeCase_DoubleClickUpdateDoesNotProduceDuplicateUpdate()
    {
        var frame = await OpenClassForEditing();
        var newLocation = $"Building 110 Room 400 - Double Click Test {Token}";
        await TrainingPage.Location(frame).FillAsync(newLocation);
        await TrainingPage.Button(frame, "Update").DblClickAsync();
        await TrainingPage.DismissDuplicateWarningIfPresentAsync(Page, frame, "Continue with update");
        await TrainingPage.ShellSaveButton(Page).ClickAsync();
        await ExpectSaved();

        var matches = await AppApi.SearchClassesAsync(Api, Token);
        Assert.That(matches, Is.EqualTo(new[] { _class with { Location = newLocation } }));
    }

    [Test]
    public async Task Search_NoResultsReturnsAppropriateMessage()
    {
        var frame = await TrainingPage.OpenSearchAsync(Page);
        await TrainingPage.SearchClassesAsync(frame, "NONEXISTENT_CLASS_XYZ_12345");
        await Expect(frame.GetByText("No results found.")).ToBeVisibleAsync();
        await Expect(TrainingPage.SearchResultRows(frame)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Search_WildcardSearchReturnsMultipleResults()
    {
        var frame = await TrainingPage.OpenSearchAsync(Page);
        await TrainingPage.SearchClassesAsync(frame, "Electrical");
        Assert.That(await TrainingPage.SearchResultRows(frame).CountAsync(), Is.GreaterThan(1));
        foreach (var course in await TrainingPage.SearchResultRows(frame).Locator("td:first-child").AllInnerTextsAsync())
            Assert.That(course, Does.Contain("Electrical"));
    }

    [Test]
    public async Task CancelEdit_ChangesAreDiscarded()
    {
        var frame = await OpenClassForEditing();
        await TrainingPage.Location(frame).FillAsync("TEMPORARY CHANGE - SHOULD NOT SAVE");
        await TrainingPage.Button(frame, "Cancel").ClickAsync();
        await Expect(TrainingPage.ShellSaveButton(Page)).ToBeHiddenAsync();

        await TrainingPage.SearchClassesAsync(frame, Token);
        await TrainingPage.OpenOnlyResultAsync(frame);
        await Expect(TrainingPage.Location(frame)).ToHaveValueAsync(_class.Location);
        Assert.That(await Saved(), Is.EqualTo(_class));
    }
}
