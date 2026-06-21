using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using SafetyOpsTests.Config;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

[TestFixture]
public class AddOMSSTests : PageTest
{
    private string? _createdAppointmentUrl;

    [SetUp]
    public async Task SetUp()
    {
        _createdAppointmentUrl = null;
        await LoginPage.Login(Page, ConfigLoader.Settings);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_createdAppointmentUrl != null)
            await CleanupCreatedOMSSRecord(Page);
    }

    private async Task CleanupCreatedOMSSRecord(IPage page)
    {
        if (_createdAppointmentUrl == null) return;

        try
        {
            await page.GotoAsync(_createdAppointmentUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            var deleteButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("delete appointment|delete record|remove appointment|remove record|delete", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).First;
            if (await deleteButton.IsVisibleAsync().ConfigureAwait(false))
            {
                await deleteButton.ClickAsync();
                var confirmButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("confirm|yes|delete|remove", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).First;
                if (await confirmButton.IsVisibleAsync().ConfigureAwait(false))
                    await confirmButton.ClickAsync();
                await page.WaitForTimeoutAsync(2_000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OMSS cleanup failed for {_createdAppointmentUrl}: {ex.Message}");
        }
        finally
        {
            _createdAppointmentUrl = null;
        }
    }

    [Test]
    public async Task FillsAndSubmitsCreateMedicalSurveillanceForm()
    {
        await OMSSPage.NavigateToOMSSCreate(Page);
        var frame = Page.FrameLocator("#frameCreate");
        await OMSSPage.PickTodayFromCalendar(frame);
        await OMSSPage.PickRandomPersonEvaluated(Page, frame);
        await OMSSPage.AddRandomWorkTask(Page, frame);
        await OMSSPage.SelectExamTypesForAllStressors(Page, frame);
        await frame.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();
        await Page.WaitForTimeoutAsync(3_000);
        var errorBanner = frame.Locator(".alert, .error, [class*='error'], [class*='alert']").First;
        bool errorVisible;
        try { errorVisible = await errorBanner.IsVisibleAsync(); } catch { errorVisible = false; }
        if (errorVisible)
        {
            var errorText = await errorBanner.TextContentAsync();
            Console.WriteLine($"Post-submit message: {errorText}");
        }
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/appointment", System.Text.RegularExpressions.RegexOptions.IgnoreCase), new() { Timeout = 5_000 });
        _createdAppointmentUrl = Page.Url;
    }
}
