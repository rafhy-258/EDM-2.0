using System.Net;
using System.Text;
using System.Text.Json;
using Edm.IntegrationTests.Support;

namespace Edm.IntegrationTests;

/// <summary>Categories and subcategories over HTTP, against PostgreSQL: rules, permissions, concurrency and audit.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ConfigurationApiTests(PostgresServer server) : IAsyncLifetime
{
    private const string Admin = "config.category.view,config.category.manage,config.subcategory.manage,audit.view";
    private const string CategoryUrl = "/api/v1/categories";
    private const string SubcategoryUrl = "/api/v1/subcategories";

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
    public async Task A_category_can_be_created_listed_and_updated()
    {
        var created = await Send(HttpMethod.Post, CategoryUrl, Admin, new { name = "  Travel  ", description = "Flights" });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal("Travel", created.Body.GetProperty("name").GetString());
        Assert.Equal(1, created.Body.GetProperty("rowVersion").GetInt64());
        var id = created.Body.GetProperty("id").GetString();

        var updated = await Send(HttpMethod.Put, $"{CategoryUrl}/{id}", Admin,
            new { name = "Air travel", description = (string?)null, isActive = true, rowVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, updated.Status);
        Assert.Equal(2, updated.Body.GetProperty("rowVersion").GetInt64());

        var list = await Send(HttpMethod.Get, CategoryUrl, Admin);
        Assert.Equal(HttpStatusCode.OK, list.Status);
        var item = Assert.Single(list.Body.EnumerateArray());
        Assert.Equal("Air travel", item.GetProperty("name").GetString());
        Assert.Equal(0, item.GetProperty("subcategoryCount").GetInt32());
    }

    [Fact]
    public async Task Stale_update_is_refused_with_412()
    {
        var created = await Send(HttpMethod.Post, CategoryUrl, Admin, new { name = "Travel" });
        var id = created.Body.GetProperty("id").GetString();
        await Send(HttpMethod.Put, $"{CategoryUrl}/{id}", Admin, new { name = "Air travel", isActive = true, rowVersion = 1 });

        var stale = await Send(HttpMethod.Put, $"{CategoryUrl}/{id}", Admin, new { name = "Rail", isActive = true, rowVersion = 1 });

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.Status);
        Assert.Equal("EDM-CONC-001", stale.Body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Duplicate_name_returns_a_field_error_on_name()
    {
        await Send(HttpMethod.Post, CategoryUrl, Admin, new { name = "Travel" });

        var duplicate = await Send(HttpMethod.Post, CategoryUrl, Admin, new { name = "TRAVEL" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.Status);
        Assert.Equal("EDM-VAL-001", duplicate.Body.GetProperty("code").GetString());
        Assert.Equal(
            "A category with this name already exists.",
            duplicate.Body.GetProperty("errors").GetProperty("name").EnumerateArray().First().GetString());
    }

    [Fact]
    public async Task Deactivated_name_can_be_reused()
    {
        var first = await Send(HttpMethod.Post, CategoryUrl, Admin, new { name = "Travel" });
        var firstId = first.Body.GetProperty("id").GetString();
        await Send(HttpMethod.Put, $"{CategoryUrl}/{firstId}", Admin, new { name = "Travel", isActive = false, rowVersion = 1 });

        var reused = await Send(HttpMethod.Post, CategoryUrl, Admin, new { name = "Travel" });

        Assert.Equal(HttpStatusCode.Created, reused.Status);
        Assert.NotEqual(firstId, reused.Body.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Requester_cannot_list_categories()
    {
        var response = await Send(HttpMethod.Get, CategoryUrl, "opex.request.create");

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Equal("EDM-AUTH-002", response.Body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Updating_a_missing_category_returns_not_found()
    {
        var response = await Send(HttpMethod.Put, $"{CategoryUrl}/{Guid.NewGuid()}", Admin, new { name = "X", rowVersion = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }

    [Fact]
    public async Task Subcategories_are_listed_for_their_category_only()
    {
        var travel = await CreateCategory("Travel");
        var training = await CreateCategory("Training");
        await Send(HttpMethod.Post, SubcategoryUrl, Admin, new { categoryId = travel, name = "Flights" });
        await Send(HttpMethod.Post, SubcategoryUrl, Admin, new { categoryId = travel, name = "Hotels" });

        var forTravel = await Send(HttpMethod.Get, $"{SubcategoryUrl}?categoryId={travel}", Admin);
        var forTraining = await Send(HttpMethod.Get, $"{SubcategoryUrl}?categoryId={training}", Admin);

        Assert.Equal(2, forTravel.Body.GetArrayLength());
        Assert.All(forTravel.Body.EnumerateArray(), s => Assert.Equal("Travel", s.GetProperty("categoryName").GetString()));
        Assert.Empty(forTraining.Body.EnumerateArray());
    }

    [Fact]
    public async Task A_subcategory_under_an_inactive_category_is_refused()
    {
        var category = await CreateCategory("Old");
        await Send(HttpMethod.Put, $"{CategoryUrl}/{category}", Admin, new { name = "Old", isActive = false, rowVersion = 1 });

        var response = await Send(HttpMethod.Post, SubcategoryUrl, Admin, new { categoryId = category, name = "Flights" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.True(response.Body.GetProperty("errors").TryGetProperty("categoryId", out _));
    }

    [Fact]
    public async Task A_category_with_subcategories_cannot_be_deleted()
    {
        var category = await CreateCategory("Travel");
        await Send(HttpMethod.Post, SubcategoryUrl, Admin, new { categoryId = category, name = "Flights" });

        var response = await Send(HttpMethod.Delete, $"{CategoryUrl}/{category}?rowVersion=1", Admin);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal("EDM-CFG-003", response.Body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_unused_category_is_deleted_and_no_longer_listed()
    {
        var category = await CreateCategory("Travel");

        var deleted = await Send(HttpMethod.Delete, $"{CategoryUrl}/{category}?rowVersion=1", Admin);
        var list = await Send(HttpMethod.Get, $"{CategoryUrl}?includeInactive=true", Admin);

        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
        Assert.Empty(list.Body.EnumerateArray());
    }

    [Fact]
    public async Task Every_change_appears_in_the_audit_viewer_with_its_values()
    {
        var category = await CreateCategory("Travel");
        await Send(HttpMethod.Put, $"{CategoryUrl}/{category}", Admin, new { name = "Air travel", isActive = true, rowVersion = 1 });

        var audit = await Send(HttpMethod.Get, "/api/v1/audit-events?module=Configuration&entityType=Category", Admin);

        Assert.Equal(HttpStatusCode.OK, audit.Status);
        var actions = audit.Body.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("action").GetString())
            .ToArray();
        Assert.Contains("Created", actions);
        Assert.Contains("Updated", actions);

        var updateEvent = audit.Body.GetProperty("items").EnumerateArray()
            .Single(e => e.GetProperty("action").GetString() == "Updated");
        var nameChange = updateEvent.GetProperty("changes").EnumerateArray()
            .Single(c => c.GetProperty("field").GetString() == "name");
        Assert.Equal("Travel", nameChange.GetProperty("before").GetString());
        Assert.Equal("Air travel", nameChange.GetProperty("after").GetString());
    }

    private async Task<string> CreateCategory(string name)
    {
        var response = await Send(HttpMethod.Post, CategoryUrl, Admin, new { name });
        return response.Body.GetProperty("id").GetString()!;
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
