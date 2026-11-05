using System.Net;
using System.Text;
using System.Text.Json;
using Edm.IntegrationTests.Support;

namespace Edm.IntegrationTests;

/// <summary>Departments and the organisation directory over HTTP, against PostgreSQL.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OrganisationApiTests(PostgresServer server) : IAsyncLifetime
{
    private const string DepartmentAdmin = "org.department.manage";
    private const string PeopleAdmin = "org.department.manage,org.user.view,org.user.manage";
    private const string PeopleViewer = "org.user.view";
    private const string Departments = "/api/v1/departments";
    private const string Users = "/api/v1/users";

    private PostgresTestDatabase _db = null!;
    private EdmApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _db = await server.CreateDatabaseAsync();
        _factory = new EdmApiFactory(_db.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task A_department_can_be_created_listed_and_renamed()
    {
        var created = await Send(HttpMethod.Post, Departments, DepartmentAdmin, new { name = "  Finance  " });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var id = created.Body.GetProperty("id").GetString();
        Assert.Equal("Finance", created.Body.GetProperty("name").GetString());

        var list = await Send(HttpMethod.Get, Departments, DepartmentAdmin);
        Assert.Single(list.Body.EnumerateArray());

        var renamed = await Send(HttpMethod.Put, $"{Departments}/{id}", DepartmentAdmin,
            new { name = "Finance Office", isActive = (bool?)null, rowVersion = 1L });
        Assert.Equal(HttpStatusCode.OK, renamed.Status);
        Assert.Equal(2, renamed.Body.GetProperty("rowVersion").GetInt64());
    }

    [Fact]
    public async Task A_duplicate_department_name_is_a_field_error()
    {
        await Send(HttpMethod.Post, Departments, DepartmentAdmin, new { name = "Finance" });

        var duplicate = await Send(HttpMethod.Post, Departments, DepartmentAdmin, new { name = "finance" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.Status);
        Assert.Equal("EDM-VAL-001", duplicate.Body.GetProperty("code").GetString());
        Assert.Equal("A department with this name already exists.",
            duplicate.Body.GetProperty("errors").GetProperty("name").EnumerateArray().First().GetString());
    }

    [Fact]
    public async Task A_stale_department_version_returns_412()
    {
        var created = await Send(HttpMethod.Post, Departments, DepartmentAdmin, new { name = "Finance" });
        var id = created.Body.GetProperty("id").GetString();
        await Send(HttpMethod.Put, $"{Departments}/{id}", DepartmentAdmin, new { name = "Finance Office", rowVersion = 1L });

        var stale = await Send(HttpMethod.Put, $"{Departments}/{id}", DepartmentAdmin, new { name = "Other", rowVersion = 1L });

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.Status);
        Assert.Equal("EDM-CONC-001", stale.Body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_department_with_active_people_cannot_be_deactivated()
    {
        var department = await Send(HttpMethod.Post, Departments, DepartmentAdmin, new { name = "Finance" });
        var departmentId = department.Body.GetProperty("id").GetString();
        await Send(HttpMethod.Post, Users, PeopleAdmin,
            new { employeeId = "emp1024", name = "Ali Khan", email = "ali@example.com", departmentId });

        var deactivate = await Send(HttpMethod.Put, $"{Departments}/{departmentId}", DepartmentAdmin,
            new { name = (string?)null, isActive = false, rowVersion = 1L });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, deactivate.Status);
        Assert.Equal("Move or deactivate the people in this department first.",
            deactivate.Body.GetProperty("errors").GetProperty("isActive").EnumerateArray().First().GetString());
    }

    [Fact]
    public async Task A_person_is_added_with_an_upper_case_employee_id()
    {
        var department = await Send(HttpMethod.Post, Departments, DepartmentAdmin, new { name = "Finance" });
        var departmentId = department.Body.GetProperty("id").GetString();

        var created = await Send(HttpMethod.Post, Users, PeopleAdmin,
            new { employeeId = "emp1024", name = "Ali Khan", email = (string?)null, departmentId });

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal("EMP1024", created.Body.GetProperty("employeeId").GetString());
        Assert.Equal(JsonValueKind.Null, created.Body.GetProperty("email").ValueKind);

        var duplicate = await Send(HttpMethod.Post, Users, PeopleAdmin,
            new { employeeId = "EMP1024", name = "Other", email = (string?)null, departmentId });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.Status);
    }

    [Fact]
    public async Task Directory_people_can_be_filtered_by_department()
    {
        var finance = await Send(HttpMethod.Post, Departments, DepartmentAdmin, new { name = "Finance" });
        var hr = await Send(HttpMethod.Post, Departments, DepartmentAdmin, new { name = "HR" });
        var financeId = finance.Body.GetProperty("id").GetString();
        var hrId = hr.Body.GetProperty("id").GetString();
        await Send(HttpMethod.Post, Users, PeopleAdmin,
            new { employeeId = "EMP1", name = "Ali Khan", email = (string?)null, departmentId = financeId });
        await Send(HttpMethod.Post, Users, PeopleAdmin,
            new { employeeId = "EMP2", name = "Sara Ahmed", email = (string?)null, departmentId = hrId });

        var inHr = await Send(HttpMethod.Get, $"{Users}?departmentId={hrId}", PeopleViewer);

        Assert.Equal(HttpStatusCode.OK, inHr.Status);
        Assert.Single(inHr.Body.EnumerateArray());
        Assert.Equal("EMP2", inHr.Body.EnumerateArray().First().GetProperty("employeeId").GetString());
    }

    [Fact]
    public async Task A_people_viewer_can_pick_active_departments_without_managing_them()
    {
        await Send(HttpMethod.Post, Departments, DepartmentAdmin, new { name = "Finance" });
        var hr = await Send(HttpMethod.Post, Departments, DepartmentAdmin, new { name = "HR" });
        var hrId = hr.Body.GetProperty("id").GetString();
        await Send(HttpMethod.Put, $"{Departments}/{hrId}", DepartmentAdmin, new { name = (string?)null, isActive = false, rowVersion = 1L });

        var options = await Send(HttpMethod.Get, $"{Departments}/options", PeopleViewer);

        Assert.Equal(HttpStatusCode.OK, options.Status);
        var only = Assert.Single(options.Body.EnumerateArray().ToArray());
        Assert.Equal("Finance", only.GetProperty("name").GetString());
        Assert.False(only.TryGetProperty("rowVersion", out _));

        var denied = await Send(HttpMethod.Get, Departments, PeopleViewer);
        Assert.Equal(HttpStatusCode.Forbidden, denied.Status);
    }

    [Fact]
    public async Task A_viewer_can_read_people_but_not_change_them()
    {
        var viewer = await Send(HttpMethod.Post, Users, PeopleViewer,
            new { employeeId = "EMP1", name = "Ali Khan", email = (string?)null, departmentId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, viewer.Status);
        Assert.Equal("EDM-AUTH-002", viewer.Body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Configuration_permissions_alone_cannot_manage_departments()
    {
        var response = await Send(HttpMethod.Get, Departments, "config.limit.manage,config.ec.pool.manage");

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
    }

    private async Task<ApiResponse> Send(HttpMethod method, string url, string permissions, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Test-User", "5b0a1c2d-0000-4000-8000-000000000042");
        request.Headers.Add("X-Test-Permissions", permissions);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        var response = await _client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var element = string.IsNullOrWhiteSpace(text)
            ? default
            : JsonDocument.Parse(text).RootElement.Clone();
        return new ApiResponse(response.StatusCode, element);
    }

    private sealed record ApiResponse(HttpStatusCode Status, JsonElement Body);
}
