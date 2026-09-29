using Microsoft.Playwright;

namespace SafetyOpsTests.Helpers;

/// <summary>
/// One test's data. Creates records through the API and remembers them, and gives the test a
/// unique <see cref="Token"/> to put in anything it creates through the UI. <see cref="CleanUpAsync"/>
/// deletes both, dependents first, so every test leaves the database as it found it.
/// </summary>
public sealed class TestDataScope(IAPIRequestContext api)
{
    private readonly List<Func<Task>> _deletes = [];

    /// <summary>Unique per test; put it in a searchable field (name, location) of UI-created records.</summary>
    public string Token { get; } = TestData.UniqueToken();

    public async Task<Person> PersonAsync(string firstName, string lastName, int? orgUnitId = null)
    {
        var person = await AppApi.CreatePersonAsync(api, firstName, lastName, orgUnitId);
        _deletes.Add(() => AppApi.DeletePersonAsync(api, person.Id));
        return person;
    }

    public async Task<TrainingClass> ClassAsync(string location, DateOnly? classDate = null)
    {
        var cls = await AppApi.CreateClassAsync(api, location, classDate);
        _deletes.Add(() => AppApi.DeleteClassAsync(api, cls.Id));
        return cls;
    }

    public async Task<Appointment> AppointmentAsync(int personId, DateOnly date, params AppointmentStressor[] stressors)
    {
        var appointment = await AppApi.CreateAppointmentAsync(api, personId, date, stressors);
        TrackAppointment(appointment.Id);
        return appointment;
    }

    public async Task<Incident> IncidentAsync(string location, string status = "Open", string category = "NearMiss", int? orgUnitId = null)
    {
        var incident = await AppApi.CreateIncidentAsync(api, location, status, category, orgUnitId: orgUnitId);
        _deletes.Add(() => AppApi.DeleteIncidentAsync(api, incident.Id));
        return incident;
    }

    /// <summary>For an appointment created through the UI for someone without the token in their name.</summary>
    public void TrackAppointment(int id) => _deletes.Add(() => AppApi.DeleteAppointmentAsync(api, id));

    /// <summary>Registers any other undo step (e.g. revoking a role granted through the UI).</summary>
    public void OnCleanUp(Func<IAPIRequestContext, Task> undo) => _deletes.Add(() => undo(api));

    /// <summary>
    /// Deletes records carrying <see cref="Token"/> (whoever created them), then tracked records in
    /// reverse creation order. Appointments and incidents go before the people they reference.
    /// Already-deleted records are fine (404s are ignored).
    /// </summary>
    public async Task CleanUpAsync()
    {
        foreach (var a in await AppApi.SearchAppointmentsAsync(api, Token)) await AppApi.DeleteAppointmentAsync(api, a.Id);
        foreach (var i in await AppApi.SearchIncidentsAsync(api, Token)) await AppApi.DeleteIncidentAsync(api, i.Id);
        foreach (var c in await AppApi.SearchClassesAsync(api, Token)) await AppApi.DeleteClassAsync(api, c.Id);

        for (var i = _deletes.Count - 1; i >= 0; i--) await _deletes[i]();
        _deletes.Clear();

        foreach (var p in await AppApi.SearchPeopleAsync(api, Token)) await AppApi.DeletePersonAsync(api, p.Id);
    }
}
