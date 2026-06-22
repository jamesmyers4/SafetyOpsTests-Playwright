# SafetyOpsTests

A C# test automation suite built with NUnit and Playwright, targeting a full-stack enterprise safety and fire management web application. This project was developed as a companion to SafetyOpsApp and demonstrates cross-language test automation skills by mirroring the architecture and coverage of an existing TypeScript/Playwright suite.

---

## Tech Stack

| Layer              | Technology                                          |
| ------------------ | --------------------------------------------------- |
| Language           | C# / .NET 10                                        |
| Test Framework     | NUnit 4.x                                           |
| Browser Automation | Microsoft.Playwright.NUnit 1.59.0                   |
| Configuration      | Microsoft.Extensions.Configuration + appsettings.json |
| IDE                | Visual Studio Community 2022                        |

---

## Project Structure

```
Config/
  ConfigLoader.cs         Loads appsettings.json and environment variables into TestSettings
  TestSettings.cs         Strongly-typed POCO: BaseUrl, credentials, Headless flag

Helpers/
  RandomName.cs           Generates standard and adversarial name inputs with weighted probability

Pages/
  LoginPage.cs            Login flow: navigates to app, authenticates, asserts post-login URL
  TrainingAdminPage.cs    TA module navigation, iframe helper, course popup, duplicate warning handler
  OMSSPage.cs             OMSS navigation, calendar picker, person/work task/exam type selection

AddUserTests.cs           Personnel Administration — add user flow
EditUserTests.cs          Personnel Administration — edit user flow
AccessLevelsTests.cs      Personnel Administration — access levels (stub)
CreateClassTests.cs       Training Administration — create class flow
EditClassTests.cs         Training Administration — edit/search class flow
AddOMSSTests.cs           Medical Surveillance — create OMSS appointment
EditOMSSTests.cs          Medical Surveillance — edit OMSS appointment

appsettings.json          Environment configuration (base URL, credentials)
SafetyOpsTest.csproj      Project file targeting .NET 10
SafetyOpsTest.slnx        Solution file
```

---

## Test Coverage

| Test Class            | Module                     | Tests |
| --------------------- | -------------------------- | ----- |
| AddUserTests.cs       | Personnel Administration   | 1     |
| EditUserTests.cs      | Personnel Administration   | 15    |
| AccessLevelsTests.cs  | Personnel Administration   | 1 ¹   |
| CreateClassTests.cs   | Training Administration    | 14    |
| EditClassTests.cs     | Training Administration    | 13    |
| AddOMSSTests.cs       | Medical Surveillance       | 1     |
| EditOMSSTests.cs      | Medical Surveillance       | 14    |
| **Total**             |                            | **59**|

¹ `AccessLevelsTests` is a stub that calls `Assert.Ignore` — mirrors the unimplemented `access-levels.spec.ts` in the TypeScript suite.

---

## Configuration

Tests are configured via `appsettings.json`. Copy the template below and fill in your local values:

```json
{
  "TestSettings": {
    "BaseUrl": "https://your-app-login-url.com",
    "SafetyOpsMainUrl": "https://your-app-url.com/n/safetyops/main",
    "FirUrl": "https://your-app-url.com/n/safetyops/fir",
    "LoginUsername": "your-username",
    "LoginPassword": "your-password",
    "Headless": true
  }
}
```

Environment variables can override any `appsettings.json` value at runtime, following standard `Microsoft.Extensions.Configuration` precedence.

---

## NuGet Dependencies

| Package                                          | Version  |
| ------------------------------------------------ | -------- |
| Microsoft.Playwright.NUnit                       | 1.59.0   |
| NUnit                                            | 4.3.2    |
| NUnit3TestAdapter                                | 5.0.0    |
| Microsoft.NET.Test.Sdk                           | 17.14.0  |
| Microsoft.Extensions.Configuration.Json         | 10.0.7   |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.7 |
| Microsoft.Extensions.Configuration.Binder       | 10.0.7   |
| coverlet.collector                               | 6.0.4    |

---

## Getting Started

### Prerequisites

- .NET 10 SDK
- Visual Studio Community 2022 with the ASP.NET workload installed
- Node.js (required by Playwright for browser binary management)

### Install Playwright Browsers

After restoring NuGet packages, install the browser binaries:

```bash
pwsh bin/Debug/net10.0/playwright.ps1 install
```

Or via the .NET CLI:

```bash
dotnet build
dotnet tool run playwright install
```

### Run Tests

From Visual Studio: Open Test Explorer and run all tests.

From the CLI:

```bash
dotnet test
```

---

## Design Patterns

**Page Object Model** — UI interactions are encapsulated in classes under `Pages/`, keeping test logic clean and selectors maintainable in one place.

**Typed Configuration** — `appsettings.json` is bound to a strongly-typed `TestSettings` class via `IConfiguration`, avoiding magic strings scattered across tests.

**NameResult Wrapper** — C# does not support TypeScript-style union types (`string | AdversarialName`), so a `NameResult` wrapper class carries either a standard or adversarial name result through the helpers cleanly.

**Soft Assertion Pattern** — Assertions that should not abort the test are wrapped in `try/catch` blocks. Failures are logged to `Console.WriteLine` with a `[soft]` prefix rather than propagated, allowing the test to continue and report partial results.

**Cleanup/Teardown Pattern** — Tests that create records store a search token or URL in a field (e.g., `_createdUserSearchToken`, `_createdClassUrl`). `TearDown` checks this field and attempts cleanup inside a `try/catch/finally`, ensuring the driver is released even if cleanup fails.

**iframe Handling** — The Training Administration and OMSS modules render inside legacy iframes. Frames are obtained via `page.Locator("iframe").Nth(0).ContentFrame` and passed to helper methods. Nested frames (the Add Work Task dialog inside the OMSS frame) use `frame.Locator("iframe").Nth(0).ContentFrame` for a second level of access.

---

## Related Repositories

- **SafetyOpsApp** — The ASP.NET Core Web API + React frontend this suite tests against.
- **Playwright-Typescript** — The original TypeScript/Playwright suite this project mirrors in coverage and test naming.
- **SafetyOpsTests-Selenium** — A parallel C#/NUnit implementation of the same coverage using Selenium WebDriver instead of Playwright.

---

## Author

James Myers — QA Engineer / SDET
