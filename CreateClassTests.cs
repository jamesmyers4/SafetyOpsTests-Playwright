using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SafetyOpsTests.Helpers;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

/// <summary>
/// Every class a test creates has the test's unique token in its location, so the base fixture's
/// cleanup finds and deletes it; no test depends on another's leftovers.
/// </summary>
[TestFixture]
public class CreateClassTests : AppPageTest
{
    private string Token => Data.Token;

    private string UniqueLocation => $"Building 110 Room 200 - {Token}";

    /// <summary>An existing Electrical - Low Voltage class dated today, so the form's duplicate check fires.</summary>
    private Task<TrainingClass> ArrangeExistingClassToday() => Data.ClassAsync($"Existing class - {Token}");

    private Task<List<TrainingClass>> SavedClasses() => AppApi.SearchClassesAsync(Api, Token);

    [Test]
    public async Task TrainingModuleLoadsAndCreateClassLinkIsVisible()
    {
        await TrainingPage.OpenAsync(Page);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Create Class" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task HappyPath_CreateClassWithAllFields()
    {
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.FillValidClassAsync(Page, frame, UniqueLocation);
        await TrainingPage.Button(frame, "Create").ClickAsync();
        await TrainingPage.DismissDuplicateWarningIfPresentAsync(Page, frame, "Continue with create");

        var id = await TrainingPage.SaveNewClassAsync(Page);

        await Expect(TrainingPage.ShellStatus(Page).Filter(new() { HasText = "Class saved successfully" })).ToBeVisibleAsync();
        await Expect(Page.GetByText($"Location: {UniqueLocation}")).ToBeVisibleAsync();
        var saved = await AppApi.GetClassAsync(Api, id);
        Assert.Multiple(() =>
        {
            Assert.That(saved.CourseId, Is.EqualTo("ELV-001"));
            Assert.That(saved.ClassDate, Is.EqualTo(DateTime.Today.ToString("yyyy-MM-dd")));
            Assert.That(saved.Location, Is.EqualTo(UniqueLocation));
            Assert.That(saved.OrgUnitName, Is.EqualTo("SafetyOps Industries"));
        });
    }

    [Test]
    public async Task Validation_SubmitCompletelyEmptyForm()
    {
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.Button(frame, "Create").ClickAsync();
        await Expect(TrainingPage.FrameAlert(frame).Locator("div"))
            .ToHaveTextAsync(["Course ID is required.", "Class Date is required.", "Specific location is required"]);
        await Expect(TrainingPage.ShellSaveButton(Page)).ToBeHiddenAsync();
    }

    [Test]
    public async Task Validation_CourseTitleIsRequired()
    {
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.PickTodayAsync(frame);
        await TrainingPage.Location(frame).FillAsync(UniqueLocation);
        await TrainingPage.Button(frame, "Create").ClickAsync();
        await Expect(TrainingPage.FrameAlert(frame)).ToHaveTextAsync("Course ID is required.");
        await Expect(TrainingPage.ShellSaveButton(Page)).ToBeHiddenAsync();
    }

    [Test]
    public async Task Validation_DateIsRequired()
    {
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.Location(frame).FillAsync(UniqueLocation);
        await TrainingPage.SelectCourseViaPopupAsync(Page, frame, TrainingPage.ElectricalLowVoltage);
        await TrainingPage.Button(frame, "Create").ClickAsync();
        await Expect(TrainingPage.FrameAlert(frame)).ToHaveTextAsync("Class Date is required.");
        await Expect(TrainingPage.ShellSaveButton(Page)).ToBeHiddenAsync();
    }

    [Test]
    public async Task EdgeCase_SpecialCharactersInLocationField()
    {
        var location = $"Bldg 110 / Room 200 & Annex <B> \"North\" {Token}";
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.FillValidClassAsync(Page, frame, location);
        await TrainingPage.Button(frame, "Create").ClickAsync();
        await TrainingPage.DismissDuplicateWarningIfPresentAsync(Page, frame, "Continue with create");
        await Expect(TrainingPage.FrameAlert(frame)).ToHaveCountAsync(0);

        var id = await TrainingPage.SaveNewClassAsync(Page);
        await Expect(Page.GetByText($"Location: {location}")).ToBeVisibleAsync();
        Assert.That((await AppApi.GetClassAsync(Api, id)).Location, Is.EqualTo(location));
    }

    [Test]
    public async Task EdgeCase_OversizedLocationInputBoundaryCheck()
    {
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.FillValidClassAsync(Page, frame, UniqueLocation);
        var longLocation = Token + new string('A', 500 - Token.Length);
        await TrainingPage.Location(frame).FillAsync(longLocation);
        // The field has no maxlength; the API is the guard (200 characters).
        await Expect(TrainingPage.Location(frame)).ToHaveValueAsync(longLocation);

        await TrainingPage.Button(frame, "Create").ClickAsync();
        await TrainingPage.DismissDuplicateWarningIfPresentAsync(Page, frame, "Continue with create");
        await TrainingPage.ShellSaveButton(Page).ClickAsync();

        await Expect(TrainingPage.ShellAlert(Page)).ToHaveTextAsync("The field Location must be a string with a maximum length of 200.");
        await Expect(Page).ToHaveURLAsync(new Regex("/training$"));
        Assert.That(await SavedClasses(), Is.Empty);
    }

    [Test]
    public async Task EdgeCase_FutureDateIsRejectedOrFlagged()
    {
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.Location(frame).FillAsync(UniqueLocation);
        await TrainingPage.SelectCourseViaPopupAsync(Page, frame, TrainingPage.ElectricalLowVoltage);
        await TrainingPage.ClassDate(frame).FillAsync("12/31/2099");
        await TrainingPage.Button(frame, "Create").ClickAsync();
        await Expect(TrainingPage.FrameAlert(frame)).ToHaveTextAsync("Future dates are not allowed.");
        await Expect(TrainingPage.ShellSaveButton(Page)).ToBeHiddenAsync();
    }

    [Test]
    public async Task EdgeCase_NonsenseDateStringIsRejected()
    {
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.Location(frame).FillAsync(UniqueLocation);
        await TrainingPage.SelectCourseViaPopupAsync(Page, frame, TrainingPage.ElectricalLowVoltage);
        await TrainingPage.ClassDate(frame).FillAsync("99/99/9999");
        await TrainingPage.Button(frame, "Create").ClickAsync();
        await Expect(TrainingPage.FrameAlert(frame)).ToHaveTextAsync("Invalid date. Please enter a valid date.");
        await Expect(TrainingPage.ShellSaveButton(Page)).ToBeHiddenAsync();
    }

    [Test]
    public async Task EdgeCase_ClosingCoursePickerPopupLeavesCourseValueEmpty()
    {
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        var popup = await Page.RunAndWaitForPopupAsync(() => frame.Locator("#course-picker-button").ClickAsync());
        await Expect(popup.GetByRole(AriaRole.Heading, new() { Name = "Select Course" })).ToBeVisibleAsync();
        await popup.CloseAsync();
        await Expect(TrainingPage.CourseTitle(frame)).ToHaveValueAsync("");
    }

    [Test]
    public async Task EdgeCase_DoubleClickCreateDoesNotProduceDuplicateClass()
    {
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.FillValidClassAsync(Page, frame, UniqueLocation);
        await TrainingPage.Button(frame, "Create").DblClickAsync();
        await TrainingPage.DismissDuplicateWarningIfPresentAsync(Page, frame, "Continue with create");
        await TrainingPage.SaveNewClassAsync(Page);

        Assert.That(await SavedClasses(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DuplicateDialog_AllThreeOptionsArePresent()
    {
        await ArrangeExistingClassToday();
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.FillValidClassAsync(Page, frame, UniqueLocation);
        await TrainingPage.Button(frame, "Create").ClickAsync();

        await Expect(frame.GetByText("A class with this course and date already exists. How would you like to proceed?")).ToBeVisibleAsync();
        await Expect(TrainingPage.Button(frame, "Continue with create")).ToBeVisibleAsync();
        await Expect(TrainingPage.Button(frame, "Go to Existing")).ToBeVisibleAsync();
        await Expect(TrainingPage.Button(frame, "Start Over")).ToBeVisibleAsync();
        await Expect(TrainingPage.ShellSaveButton(Page)).ToBeHiddenAsync();
    }

    [Test]
    public async Task DuplicateDialog_ContinueWithCreateProceedsToSave()
    {
        await ArrangeExistingClassToday();
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.FillValidClassAsync(Page, frame, UniqueLocation);
        await TrainingPage.Button(frame, "Create").ClickAsync();

        Assert.That(await TrainingPage.DismissDuplicateWarningIfPresentAsync(Page, frame, "Continue with create"), Is.True);
        await TrainingPage.SaveNewClassAsync(Page);
        await Expect(TrainingPage.ShellStatus(Page).Filter(new() { HasText = "Class saved successfully" })).ToBeVisibleAsync();
        Assert.That(await SavedClasses(), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task DuplicateDialog_GoToExistingOpensTheExistingClass()
    {
        await ArrangeExistingClassToday();
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.FillValidClassAsync(Page, frame, UniqueLocation);
        await TrainingPage.Button(frame, "Create").ClickAsync();
        await TrainingPage.Button(frame, "Go to Existing").ClickAsync();

        // The frame posts trainingGoToExisting; the shell navigates to that class.
        await Page.WaitForURLAsync(new Regex(@"/training/classes/\d+$"));
        var existing = await AppApi.GetClassAsync(Api, TrainingPage.ClassIdFromUrl(Page.Url));
        Assert.Multiple(() =>
        {
            Assert.That(existing.CourseId, Is.EqualTo("ELV-001"));
            Assert.That(existing.ClassDate, Is.EqualTo(DateTime.Today.ToString("yyyy-MM-dd")));
        });
        Assert.That(await SavedClasses(), Has.Count.EqualTo(1), "Go to Existing must not create a class");
    }

    [Test]
    public async Task DuplicateDialog_StartOverResetsTheForm()
    {
        await ArrangeExistingClassToday();
        var frame = await TrainingPage.OpenCreateClassAsync(Page);
        await TrainingPage.FillValidClassAsync(Page, frame, UniqueLocation);
        await TrainingPage.Button(frame, "Create").ClickAsync();
        await TrainingPage.Button(frame, "Start Over").ClickAsync();

        await Expect(TrainingPage.Location(frame)).ToHaveValueAsync("");
        await Expect(TrainingPage.CourseTitle(frame)).ToHaveValueAsync("");
        await Expect(TrainingPage.ClassDate(frame)).ToHaveValueAsync("");
        await Expect(TrainingPage.Button(frame, "Start Over")).ToBeHiddenAsync();
        await Expect(TrainingPage.ShellSaveButton(Page)).ToBeHiddenAsync();
    }
}
