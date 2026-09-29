using Microsoft.Playwright;
using SafetyOpsTests.Helpers;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

/// <summary>
/// Each test edits its own appointment: a person and an appointment (30 days ago, one stressor)
/// are created through the API with a unique token in the person's name.
/// Tests find the record by that token instead of relying on seed data.
/// </summary>
[TestFixture]
public class EditAppointmentTests : AppPageTest
{
    private Appointment _appointment = null!;

    private string Token => Data.Token;

    private static string Today => DateTime.Today.ToString("yyyy-MM-dd");

    [SetUp]
    public async Task CreateAppointmentToEdit()
    {
        var person = await Data.PersonAsync("Medical", $"Patient {Token}");
        _appointment = await Data.AppointmentAsync(person.Id,
            DateOnly.FromDateTime(DateTime.Today).AddDays(-30), new AppointmentStressor("STR-001", "", "Initial"));
    }

    private async Task<IFrameLocator> OpenAppointmentForEditing(string? search = null)
    {
        await MedicalSurveillancePage.OpenSearchAsync(Page);
        await MedicalSurveillancePage.SearchAsync(Page, search ?? Token);
        return await MedicalSurveillancePage.OpenOnlyResultAsync(Page);
    }

    private async Task UpdateAndExpectSaved(IFrameLocator frame)
    {
        await MedicalSurveillancePage.Button(frame, "Update").ClickAsync();
        // The frame confirms, and posts appointmentUpdated so the page shows the same message.
        await Expect(frame.GetByRole(AriaRole.Status).Filter(new() { HasText = MedicalSurveillancePage.UpdatedMessage })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = MedicalSurveillancePage.UpdatedMessage })).ToBeVisibleAsync();
        await Expect(MedicalSurveillancePage.FrameAlert(frame)).ToHaveCountAsync(0);
    }

    private Task<Appointment> Saved() => AppApi.GetAppointmentAsync(Api, _appointment.Id);

    private static Dictionary<string, string> ExamTypes(Appointment a) => a.Stressors.ToDictionary(s => s.StressorId, s => s.ExamType);

    [Test]
    public async Task MedicalSurveillanceEditModuleLoadsAndSearchLinkIsVisible()
    {
        await MedicalSurveillancePage.OpenAsync(Page);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Edit / Search" })).ToBeVisibleAsync();
        await MedicalSurveillancePage.OpenSearchAsync(Page);
    }

    [Test]
    public async Task HappyPath_SearchAndEditAppointmentDate()
    {
        var frame = await OpenAppointmentForEditing();
        await MedicalSurveillancePage.PickTodayFromCalendarAsync(frame);
        await UpdateAndExpectSaved(frame);
        Assert.That((await Saved()).Date, Is.EqualTo(Today));
    }

    [Test]
    public async Task HappyPath_SearchAndEditPersonEvaluated()
    {
        var frame = await OpenAppointmentForEditing();
        var person = await MedicalSurveillancePage.PickRandomPersonEvaluatedAsync(Page, frame, except: _appointment.PersonName);
        await UpdateAndExpectSaved(frame);
        Assert.That((await Saved()).PersonName, Is.EqualTo(person));
    }

    [Test]
    public async Task HappyPath_SearchAndEditExamTypes()
    {
        var frame = await OpenAppointmentForEditing();
        var examTypes = await MedicalSurveillancePage.SelectExamTypesForAllStressorsAsync(frame);
        await UpdateAndExpectSaved(frame);
        Assert.That(ExamTypes(await Saved()), Is.EquivalentTo(examTypes));
    }

    [Test]
    public async Task Validation_SearchWithNoResultsReturnsMessage()
    {
        await MedicalSurveillancePage.OpenSearchAsync(Page);
        await MedicalSurveillancePage.SearchAsync(Page, "NONEXISTENT_RECORD_XYZ_12345");
        await Expect(Page.GetByText("No results found. No appointments match your search.")).ToBeVisibleAsync();
        await Expect(MedicalSurveillancePage.SearchResultRows(Page)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Validation_SearchWithPartialTermReturnsMultipleResults()
    {
        // "Smith" is part of every seeded appointment's person (John, Jane, and Robert Smith).
        await MedicalSurveillancePage.OpenSearchAsync(Page);
        await MedicalSurveillancePage.SearchAsync(Page, "Smith");
        Assert.That(await MedicalSurveillancePage.SearchResultRows(Page).CountAsync(), Is.GreaterThan(1));
        foreach (var person in await MedicalSurveillancePage.SearchResultRows(Page).Locator("td:nth-child(2)").AllInnerTextsAsync())
            Assert.That(person, Does.Contain("Smith"));
    }

    [Test]
    public async Task SearchForTheRemovedAppointmentKeywordReturnsNothing()
    {
        // The old app returned every record for "appointment(s)"; the rebuilt search doesn't.
        await MedicalSurveillancePage.OpenSearchAsync(Page);
        await MedicalSurveillancePage.SearchAsync(Page, "appointments");
        await Expect(Page.GetByText("No results found. No appointments match your search.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task EdgeCase_UpdateOnlyDateField()
    {
        var frame = await OpenAppointmentForEditing();
        await MedicalSurveillancePage.PickTodayFromCalendarAsync(frame);
        await UpdateAndExpectSaved(frame);
        await Expect(MedicalSurveillancePage.PersonEvaluated(frame)).ToHaveValueAsync(_appointment.PersonName);
        var saved = await Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.Date, Is.EqualTo(Today));
            Assert.That(saved.PersonId, Is.EqualTo(_appointment.PersonId));
            Assert.That(ExamTypes(saved), Is.EquivalentTo(ExamTypes(_appointment)));
        });
    }

    [Test]
    public async Task EdgeCase_UpdateOnlyPersonEvaluatedField()
    {
        var frame = await OpenAppointmentForEditing();
        var person = await MedicalSurveillancePage.PickRandomPersonEvaluatedAsync(Page, frame, except: _appointment.PersonName);
        await UpdateAndExpectSaved(frame);
        var saved = await Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.PersonName, Is.EqualTo(person));
            Assert.That(saved.Date, Is.EqualTo(_appointment.Date));
            Assert.That(ExamTypes(saved), Is.EquivalentTo(ExamTypes(_appointment)));
        });
    }

    [Test]
    public async Task EdgeCase_UpdateOnlyExamTypesField()
    {
        var frame = await OpenAppointmentForEditing();
        var examTypes = await MedicalSurveillancePage.SelectExamTypesForAllStressorsAsync(frame);
        await UpdateAndExpectSaved(frame);
        var saved = await Saved();
        Assert.Multiple(() =>
        {
            Assert.That(ExamTypes(saved), Is.EquivalentTo(examTypes));
            Assert.That(saved.Date, Is.EqualTo(_appointment.Date));
            Assert.That(saved.PersonId, Is.EqualTo(_appointment.PersonId));
        });
    }

    [Test]
    public async Task EdgeCase_AddWorkTaskDuringEdit()
    {
        var frame = await OpenAppointmentForEditing();
        await MedicalSurveillancePage.AddRandomWorkTaskAsync(frame);
        var shown = await MedicalSurveillancePage.StressorRows(frame).Locator("td:first-child").AllInnerTextsAsync();
        await UpdateAndExpectSaved(frame);

        var saved = await Saved();
        Assert.That(saved.Stressors.Select(s => s.StressorId), Is.EquivalentTo(shown.Select(s => s.Trim())));
        Assert.That(saved.Stressors.Single(s => s.StressorId == "STR-001").ExamType, Is.EqualTo("Initial"));
    }

    [Test]
    public async Task EdgeCase_DoubleClickUpdateDoesNotCreateDuplicateSaves()
    {
        var frame = await OpenAppointmentForEditing();
        await MedicalSurveillancePage.PickTodayFromCalendarAsync(frame);
        await MedicalSurveillancePage.Button(frame, "Update").DblClickAsync();
        await Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = MedicalSurveillancePage.UpdatedMessage })).ToBeVisibleAsync();
        await Expect(MedicalSurveillancePage.FrameAlert(frame)).ToHaveCountAsync(0);

        var matches = await AppApi.SearchAppointmentsAsync(Api, Token);
        Assert.That(matches.Select(a => a.Id), Is.EqualTo(new[] { _appointment.Id }));
        Assert.That(matches[0].Date, Is.EqualTo(Today));
    }

    [Test]
    public async Task CancelEdit_ChangesAreDiscarded()
    {
        var frame = await OpenAppointmentForEditing();
        var originalDate = await MedicalSurveillancePage.AppointmentDate(frame).InputValueAsync();
        await MedicalSurveillancePage.PickTodayFromCalendarAsync(frame);
        await MedicalSurveillancePage.Button(frame, "Cancel").ClickAsync();
        // Cancel posts appointmentEditCancelled and the page closes the frame.
        await Expect(Page.Locator("#edit-frame")).ToHaveCountAsync(0);

        await MedicalSurveillancePage.SearchAsync(Page, Token);
        var reopened = await MedicalSurveillancePage.OpenOnlyResultAsync(Page);
        await Expect(MedicalSurveillancePage.AppointmentDate(reopened)).ToHaveValueAsync(originalDate);
        Assert.That(await Saved(), Is.EqualTo(_appointment).Using<Appointment>(SameRecord));
    }

    [Test]
    public async Task MultipleFieldUpdate_DatePersonAndExamTypes()
    {
        var frame = await OpenAppointmentForEditing();
        await MedicalSurveillancePage.PickTodayFromCalendarAsync(frame);
        var person = await MedicalSurveillancePage.PickRandomPersonEvaluatedAsync(Page, frame, except: _appointment.PersonName);
        var examTypes = await MedicalSurveillancePage.SelectExamTypesForAllStressorsAsync(frame);
        await UpdateAndExpectSaved(frame);

        var saved = await Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.Date, Is.EqualTo(Today));
            Assert.That(saved.PersonName, Is.EqualTo(person));
            Assert.That(ExamTypes(saved), Is.EquivalentTo(examTypes));
        });
    }

    [Test]
    public async Task VerifyRecordReflectsChangesAfterEdit()
    {
        var frame = await OpenAppointmentForEditing();
        await MedicalSurveillancePage.PickTodayFromCalendarAsync(frame);
        await UpdateAndExpectSaved(frame);

        // Search by the record's id (an exact match) and reopen it from scratch.
        await MedicalSurveillancePage.OpenSearchAsync(Page);
        await MedicalSurveillancePage.SearchAsync(Page, _appointment.Id.ToString());
        await Expect(MedicalSurveillancePage.SearchResultRows(Page).Locator("td").First).ToHaveTextAsync(_appointment.Id.ToString());
        var reopened = await MedicalSurveillancePage.OpenOnlyResultAsync(Page);
        await Expect(MedicalSurveillancePage.AppointmentDate(reopened)).ToHaveValueAsync(DateTime.Today.ToString("MM/dd/yyyy"));
    }

    /// <summary>Records compare by value, but their stressor lists are reference types.</summary>
    private static bool SameRecord(Appointment a, Appointment b) =>
        a with { Stressors = null! } == b with { Stressors = null! } && a.Stressors.SequenceEqual(b.Stressors);
}
