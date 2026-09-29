using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SafetyOpsTests.Helpers;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

[TestFixture]
public class CreateAppointmentTests : AppPageTest
{
    [Test]
    public async Task FillsAndSubmitsCreateMedicalSurveillanceForm()
    {
        var frame = await MedicalSurveillancePage.OpenCreateAsync(Page);
        await MedicalSurveillancePage.PickTodayFromCalendarAsync(frame);
        var person = await MedicalSurveillancePage.PickRandomPersonEvaluatedAsync(Page, frame);
        await MedicalSurveillancePage.AddRandomWorkTaskAsync(frame);
        var examTypes = await MedicalSurveillancePage.SelectExamTypesForAllStressorsAsync(frame);
        await MedicalSurveillancePage.Button(frame, "Create").ClickAsync();

        // The frame sends the whole page to the new appointment.
        await Page.WaitForURLAsync(new Regex(@"/medical-surveillance/appointments/\d+$"));
        var id = MedicalSurveillancePage.AppointmentIdFromUrl(Page.Url);
        Data.TrackAppointment(id); // the randomly picked person doesn't carry the token
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Appointment Details" })).ToBeVisibleAsync();
        await Expect(Page.GetByText($"Person Evaluated: {person}")).ToBeVisibleAsync();

        var saved = await AppApi.GetAppointmentAsync(Api, id);
        Assert.Multiple(() =>
        {
            Assert.That(saved.Date, Is.EqualTo(DateTime.Today.ToString("yyyy-MM-dd")));
            Assert.That(saved.PersonName, Is.EqualTo(person));
            Assert.That(saved.Stressors.ToDictionary(s => s.StressorId, s => s.ExamType), Is.EquivalentTo(examTypes));
        });
    }

    [Test]
    public async Task CreateRequiresDateAndPerson()
    {
        var frame = await MedicalSurveillancePage.OpenCreateAsync(Page);
        await MedicalSurveillancePage.Button(frame, "Create").ClickAsync();
        await Expect(MedicalSurveillancePage.FrameAlert(frame).Locator("div"))
            .ToHaveTextAsync(["Appointment Date is required.", "Person Evaluated is required."]);
        await Expect(Page).ToHaveURLAsync(new Regex("/medical-surveillance/create$"));
    }

    [Test]
    public async Task DeleteAppointmentFromDetailsPageAsksForConfirmation()
    {
        var person = await Data.PersonAsync("Medical", $"Delete {Data.Token}");
        var appointment = await Data.AppointmentAsync(person.Id, DateOnly.FromDateTime(DateTime.Today));

        await Page.GotoAsync($"/medical-surveillance/appointments/{appointment.Id}");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete Appointment" }).ClickAsync();
        await Expect(Page.GetByText("Confirm deletion?")).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Appointment deleted successfully." })).ToBeVisibleAsync();
        Assert.That(await AppApi.SearchAppointmentsAsync(Api, Data.Token), Is.Empty);
    }
}
