// Mirrors: Playwright-Typescript/tests/e2e/personnel-admin/access-levels.spec.ts (stub — no tests written yet)
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using SafetyOpsTests.Config;
using SafetyOpsTests.Pages;

namespace SafetyOpsTests.Tests;

[TestFixture]
public class AccessLevelsTests : PageTest
{
    [SetUp]
    public async Task SetUp()
    {
        await LoginPage.Login(Page, ConfigLoader.Settings);
    }

    [Test]
    public void AccessLevels_NotYetImplemented()
    {
        Assert.Ignore("Not yet implemented — mirrors access-levels.spec.ts stub");
    }
}
