# SafetyOpsTests-Playwright

[![E2E](https://github.com/jamesmyers4/SafetyOpsTests-Playwright/actions/workflows/e2e.yml/badge.svg)](https://github.com/jamesmyers4/SafetyOpsTests-Playwright/actions/workflows/e2e.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

An end-to-end test suite in C# (NUnit + Playwright for .NET) for [SafetyOpsApp](https://github.com/jamesmyers4/SafetyOpsApp), a workplace-safety records system with role-based access control across an org hierarchy. The app's UI is built to be hard to automate: forms live in iframe shells whose **Save** button sits on the parent page, pickers open in **popup windows**, one picker is an **iframe nested inside an iframe**, and frames talk to their parents through origin-checked **`postMessage`**. The suite has 108 tests. They run headless in CI against the app started from a pinned commit in Docker, and every test creates and deletes its own data.

## What this demonstrates

- **Frames, popups, and messaging.** `FrameLocator`s for shell iframes and a nested work-task picker; `RunAndWaitForPopupAsync` for course/person pickers that post their choice back to the opener and close; assertions on both sides of a `postMessage` handoff (the frame's status and the parent page's banner).
- **Multi-user RBAC tests.** Viewer, Manager, and Admin sessions side by side. An Admin grants a role in one browser context, and the grantee's already-open session picks it up on its next load. Also covered: hidden UI actions per role, the API returning 403/404, and scoped org-unit pickers.
- **Isolated, self-cleaning test data.** Tests sign in through the API (the cookie is shared with pages, frames, and popups) and arrange data through it. Each test carries a unique token, and a per-test `TestDataScope` deletes everything it created, dependents first, pass or fail. An assembly-level check compares record counts before and after the run and fails the run if anything leaked.
- **Assertions with an oracle.** Randomized and adversarial names (SQL/XSS probes, diacritics, RTL, 255-character strings) are still used. The expected outcome is derived from the input (required field, 100-character limit, or success), then checked in the UI and against what the API actually saved.
- **Debuggable failures.** Every test records a Playwright trace. Failed tests keep the trace and a full-page screenshot, and CI uploads them with the TRX and the app's container logs.

## Run it in 60 seconds

Prerequisites: .NET 10 SDK, Docker, PowerShell 7 (for Playwright's install script).

```bash
# 1. Start the app with fresh demo data on http://localhost:8080
git clone https://github.com/jamesmyers4/SafetyOpsApp
cd SafetyOpsApp && docker compose up -d --build && cd ..

# 2. Build, install Chromium, and run the suite
git clone https://github.com/jamesmyers4/SafetyOpsTests-Playwright
cd SafetyOpsTests-Playwright
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test
```

`docker compose down -v` (in the app folder) resets the app to fresh demo data. The suite also passes repeatedly against the same database.

To watch it run, use `HEADED=1 dotnet test` (or set `TestSettings__Headless=false`). To open a failure's trace: `pwsh bin/Debug/net10.0/playwright.ps1 show-trace bin/Debug/net10.0/playwright-traces/<test>.zip`.

## Coverage

| Fixture | Area | Tests |
| ------- | ---- | ----: |
| `AuthTests` | Sign-in guard and `returnUrl` (pages and iframe pages), bad credentials, open-redirect protection, sign-out, API 401s | 14 |
| `NavigationTests` | UI sign-in, Modules menu | 6 |
| `AddUserTests` | Add User form (randomized/adversarial names), server validation, delete with `window.confirm` | 4 |
| `EditUserTests` | Search, edit every field, validation, cancel, double-submit | 15 |
| `AccessLevelsTests` | Org tree per role, read-only Viewer, Manager's scoped pickers, Admin grant → grantee → revoke | 10 |
| `CreateClassTests` | Create-class frame, course popup, duplicate dialog (continue / go to existing / start over), date rules, shell Save | 15 |
| `EditClassTests` | Search/edit frame, validation, 200-char limit surfaced by the shell, cancel, double-submit | 13 |
| `CreateAppointmentTests` | Create frame: calendar, person popup, nested work-task frame, exam types; required fields; delete | 3 |
| `EditAppointmentTests` | Search, edit date/person/exam types/work tasks, `postMessage` confirm and cancel, search by id | 15 |
| `IncidentTests` | Create, validation, future-time rejection, search, status/category filters, edit, delete, Viewer scope | 13 |
| **Total** | | **108** |

## Project layout

```
AppPageTest.cs        Base fixture: BaseURL, API sign-in, per-test Admin API session, test data, traces on failure
AssemblySetup.cs      Headless setting; record-count leak check around the whole run
Config/               TestSettings (BaseUrl, Username, Password, Headless) from appsettings.json + env vars
Helpers/
  AppApi.cs           Typed wrapper over the app's REST API (arrange, verify, clean up)
  TestDataScope.cs    Per-test token, tracked creates, dependency-ordered cleanup
  ApiSession.cs       Cookie sign-in for a browser context or API context
  DemoUsers.cs        Seeded admin / manager / viewer accounts
  OrgUnits.cs         The app's fixed org tree
  RandomName.cs       Plain and adversarial name generator
Pages/                Page objects: LoginPage, Navigation, PersonnelPage, AccessLevelsPage,
                      TrainingPage, MedicalSurveillancePage, IncidentsPage
*Tests.cs             Test fixtures (see Coverage)
.github/workflows/    E2E workflow: SafetyOpsApp (pinned) in Docker → dotnet test → artifacts
```

## Configuration

`appsettings.json` points at the Docker setup and the seeded demo Admin (public demo credentials, not secrets):

```json
{
  "TestSettings": {
    "BaseUrl": "http://localhost:8080",
    "Username": "admin",
    "Password": "admin",
    "Headless": true
  }
}
```

Environment variables override it: `TestSettings__BaseUrl` (e.g. `https://localhost:14418` for the app's dev profile; HTTPS errors are ignored), `TestSettings__Username`, `TestSettings__Password`, `TestSettings__Headless`.

## Found while testing

The suite pins down current behavior. Where that behavior looks wrong, the test says so in a comment, and the issue is reported to the app rather than hidden:

- Edit Class: clearing the Course Title box doesn't clear the course. The save reports success and keeps the old course, with no "Course ID is required." message.
- Add User shows raw server messages for missing names; Edit User validates on the client.
- Person names aren't trimmed on save.

## Tech stack

C# / .NET 10 · NUnit 5 · Microsoft.Playwright.NUnit 1.63 (Chromium) · Microsoft.Extensions.Configuration · GitHub Actions + Docker Compose

## Related

- [SafetyOpsApp](https://github.com/jamesmyers4/SafetyOpsApp): the system under test (ASP.NET Core 10 API + React), with its own 171 API tests.

## License

[MIT](LICENSE) © James Myers
