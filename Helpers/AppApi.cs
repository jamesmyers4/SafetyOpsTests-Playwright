using System.Text.Json;
using Microsoft.Playwright;

namespace SafetyOpsTests.Helpers;

public record Paged<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public record Person(
    int Id, string FirstName, string LastName, string MiddleName, string Gender, string Department,
    string EmployeeCategory, string Subscription, string EmployeeNumber, int OrgUnitId, string OrgUnitName);

/// <summary>
/// Thin JSON wrapper over the app's REST API, used for test setup, verification, and cleanup.
/// Pass <c>Context.APIRequest</c> so calls carry the signed-in user's cookie.
/// </summary>
public static class AppApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<T> GetAsync<T>(IAPIRequestContext api, string url) =>
        await ReadAsync<T>(await api.GetAsync(url), "GET", url);

    public static async Task<T> PostAsync<T>(IAPIRequestContext api, string url, object body) =>
        await ReadAsync<T>(await api.PostAsync(url, new() { DataObject = body }), "POST", url);

    public static async Task DeleteAsync(IAPIRequestContext api, string url)
    {
        var response = await api.DeleteAsync(url);
        if (!response.Ok && response.Status != 404)
            throw new InvalidOperationException($"DELETE {url} failed: {response.Status} {await response.TextAsync()}");
    }

    private static async Task<T> ReadAsync<T>(IAPIResponse response, string method, string url)
    {
        var text = await response.TextAsync();
        if (!response.Ok) throw new InvalidOperationException($"{method} {url} failed: {response.Status} {text}");
        return JsonSerializer.Deserialize<T>(text, Json)!;
    }

    // Personnel

    public static Task<Person> CreatePersonAsync(IAPIRequestContext api, string firstName, string lastName, int? orgUnitId = null) =>
        PostAsync<Person>(api, "/api/personnel", new
        {
            firstName, lastName, middleName = "", gender = "", department = "Safety",
            employeeCategory = "Full Time", subscription = "Basic", employeeNumber = "1234567", orgUnitId,
        });

    public static async Task<List<Person>> SearchPeopleAsync(IAPIRequestContext api, string search) =>
        (await GetAsync<Paged<Person>>(api, $"/api/personnel?pageSize=100&search={Uri.EscapeDataString(search)}")).Items;

    public static Task<Person> GetPersonAsync(IAPIRequestContext api, int id) => GetAsync<Person>(api, $"/api/personnel/{id}");

    public static Task DeletePersonAsync(IAPIRequestContext api, int id) => DeleteAsync(api, $"/api/personnel/{id}");

    // Training

    /// <summary>Creates a class; <paramref name="classDate"/> defaults to today.</summary>
    public static Task<TrainingClass> CreateClassAsync(IAPIRequestContext api, string location, DateOnly? classDate = null, string courseId = "ELV-001", int? orgUnitId = null) =>
        PostAsync<TrainingClass>(api, "/api/training/classes", new
        {
            courseId, classDate = (classDate ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd"), location, orgUnitId,
        });

    public static async Task<List<TrainingClass>> SearchClassesAsync(IAPIRequestContext api, string search) =>
        (await GetAsync<Paged<TrainingClass>>(api, $"/api/training/classes?pageSize=100&search={Uri.EscapeDataString(search)}")).Items;

    public static Task<TrainingClass> GetClassAsync(IAPIRequestContext api, int id) => GetAsync<TrainingClass>(api, $"/api/training/classes/{id}");

    public static Task DeleteClassAsync(IAPIRequestContext api, int id) => DeleteAsync(api, $"/api/training/classes/{id}");

    // Medical surveillance

    public static Task<Appointment> CreateAppointmentAsync(IAPIRequestContext api, int personId, DateOnly date, params AppointmentStressor[] stressors) =>
        PostAsync<Appointment>(api, "/api/medical-surveillance/appointments", new
        {
            date = date.ToString("yyyy-MM-dd"), personId,
            stressors = stressors.Select(s => new { stressorId = s.StressorId, examType = s.ExamType }),
        });

    public static async Task<List<Appointment>> SearchAppointmentsAsync(IAPIRequestContext api, string search) =>
        (await GetAsync<Paged<Appointment>>(api, $"/api/medical-surveillance/appointments?pageSize=100&search={Uri.EscapeDataString(search)}")).Items;

    public static Task<Appointment> GetAppointmentAsync(IAPIRequestContext api, int id) => GetAsync<Appointment>(api, $"/api/medical-surveillance/appointments/{id}");

    public static Task DeleteAppointmentAsync(IAPIRequestContext api, int id) => DeleteAsync(api, $"/api/medical-surveillance/appointments/{id}");

    // Incident reports

    /// <summary>Creates an incident reported by the first person matching <paramref name="reporter"/>, yesterday at 09:30.</summary>
    public static async Task<Incident> CreateIncidentAsync(IAPIRequestContext api, string location, string status = "Open",
        string category = "NearMiss", string severity = "Low", string reporter = "John Smith", int? orgUnitId = null)
    {
        var reportedBy = (await GetAsync<List<PersonOption>>(api, $"/api/personnel/lookup?search={Uri.EscapeDataString(reporter)}")).First();
        return await PostAsync<Incident>(api, "/api/incidents", new
        {
            occurredAt = DateTime.Today.AddDays(-1).AddHours(9.5).ToString("yyyy-MM-ddTHH:mm"),
            location, category, severity, description = $"Test incident at {location}", reportedById = reportedBy.Id, status, orgUnitId,
        });
    }

    public static async Task<List<Incident>> SearchIncidentsAsync(IAPIRequestContext api, string search) =>
        (await GetAsync<Paged<Incident>>(api, $"/api/incidents?pageSize=100&search={Uri.EscapeDataString(search)}")).Items;

    public static Task<Incident> GetIncidentAsync(IAPIRequestContext api, int id) => GetAsync<Incident>(api, $"/api/incidents/{id}");

    public static Task DeleteIncidentAsync(IAPIRequestContext api, int id) => DeleteAsync(api, $"/api/incidents/{id}");

    // Access

    public static Task<List<RoleAssignment>> ListRoleAssignmentsAsync(IAPIRequestContext api) =>
        GetAsync<List<RoleAssignment>>(api, "/api/access/assignments");

    public static Task RevokeRoleAsync(IAPIRequestContext api, int assignmentId) => DeleteAsync(api, $"/api/access/assignments/{assignmentId}");
}

public record PersonOption(int Id, string Name);

public record Incident(
    int Id, string OccurredAt, string Location, string Category, string Severity, string Description,
    int ReportedById, string ReportedByName, string Status, int OrgUnitId, string OrgUnitName);

public record RoleAssignment(int Id, int UserId, string UserName, string DisplayName, int OrgUnitId, string OrgUnitName, string Role);

public record Appointment(int Id, string Date, int PersonId, string PersonName, List<AppointmentStressor> Stressors, int OrgUnitId, string OrgUnitName);

public record AppointmentStressor(string StressorId, string StressorName, string ExamType);

public record TrainingClass(int Id, string CourseTitle, string CourseId, string ClassDate, string Location, int OrgUnitId, string OrgUnitName);
