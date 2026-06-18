SafetyOpsTests

A C# test automation suite built with NUnit and Playwright, targeting a full-stack enterprise safety and fire management web application. This project was developed as a companion to SafetyOpsApp and demonstrates cross-language test automation skills by mirroring the architecture and coverage of an existing TypeScript/Playwright suite.


Tech Stack

LayerTechnologyLanguageC# / .NET 10Test FrameworkNUnitBrowser AutomationMicrosoft.Playwright.NUnitConfigurationMicrosoft.Extensions.Configuration + appsettings.jsonIDEVisual Studio Community 2022


Project Structure

```
Config/
  TestConfig.cs              Binds appsettings.json to a typed config object
Helpers/
  RandomName.cs              Generates standard and adversarial name inputs
Pages/
  LoginPage.cs               Page object model for the login flow
AddUserTests.cs              NUnit test class covering user creation scenarios
appsettings.json             Environment configuration (base URL, credentials)
EsamsTest.csproj             Project file targeting .NET 10
EsamsTest.slnx               Solution file
```

Configuration

Tests are configured via appsettings.json. Copy the template below and fill in your local values:

json{
  "TestSettings": {
    "BaseUrl": "https://localhost:7075",
    "Username": "your-test-username",
    "Password": "your-test-password"
  }
}

Environment variables can override any appsettings.json value at runtime, following standard Microsoft.Extensions.Configuration precedence.


NuGet Dependencies

Microsoft.Playwright.NUnit
Microsoft.Extensions.Configuration.Json
Microsoft.Extensions.Configuration.EnvironmentVariables
Microsoft.Extensions.Configuration.Binder


Getting Started

Prerequisites


.NET 10 SDK
Visual Studio Community 2022 with the ASP.NET workload installed
Node.js (required by Playwright for browser binary management)


Install Playwright Browsers

After restoring NuGet packages, install the browser binaries:

bashpwsh bin/Debug/net10.0/playwright.ps1 install

Or via the .NET CLI:

bashdotnet build
dotnet tool run playwright install

Run Tests

From Visual Studio: Open Test Explorer and run all tests.

From the CLI:

bashdotnet test


Design Patterns

Page Object Model — UI interactions are encapsulated in classes under Pages/, keeping test logic clean and selectors maintainable in one place.

Typed Configuration — appsettings.json is bound to a strongly-typed TestConfig class via IConfiguration, avoiding magic strings scattered across tests.

NameResult Wrapper — C# does not support TypeScript-style union types (string | AdversarialName), so a NameResult wrapper class is used to carry either a standard or adversarial name result through the helpers cleanly.


Related Repository

SafetyOpsApp — The ASP.NET Core Web API + React frontend this suite tests against.


Author

James Myers — QA Engineer / SDET
