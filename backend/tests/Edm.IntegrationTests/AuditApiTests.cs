using System.Net;
using System.Text.Json;
using Edm.IntegrationTests.Support;
using Edm.SharedKernel.Permissions;
using Edm.Web.Authorization;
using Edm.Web.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Edm.IntegrationTests;

/// <summary>The audit viewer and the pipeline around it: auth, problem details, correlation, validation.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AuditApiTests(PostgresServer server) : IAsyncLifetime
{
    private const string ListUrl = "/api/v1/audit-events";
    private static readonly string[] AnonymousAllowList = ["/health/live", "/health/ready"];

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
    public async Task Liveness_is_anonymous_and_healthy()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_reports_the_database_as_reachable()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Without_a_sign_in_the_viewer_answers_401_with_EDM_AUTH_001()
    {
        var response = await _client.GetAsync(ListUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.Equal("EDM-AUTH-001", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_signed_in_user_without_audit_view_gets_403_with_EDM_AUTH_002()
    {
        var request = Request(ListUrl, permissions: "org.user.view");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("EDM-AUTH-002", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_auditor_sees_a_page_and_the_view_is_itself_audited()
    {
        var first = await _client.SendAsync(Request(ListUrl, permissions: "audit.view", correlation: "corr-view-1"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("corr-view-1", first.Headers.GetValues(CorrelationKeys.HeaderName).Single());
        var page = await ReadJsonAsync(first);
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 0);

        var second = await _client.SendAsync(Request(ListUrl, permissions: "audit.view", correlation: "corr-view-2"));
        var body = await ReadJsonAsync(second);

        var views = body.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("action").GetString() == "VIEW")
            .ToList();
        Assert.NotEmpty(views);
        var firstView = views.First();
        Assert.Equal("Test User", firstView.GetProperty("actorName").GetString());
        Assert.Equal("corr-view-1", firstView.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task A_page_size_above_the_limit_is_refused_with_field_errors()
    {
        var response = await _client.SendAsync(Request($"{ListUrl}?pageSize=500", permissions: "audit.view"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("EDM-VAL-001", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("pageSize", out _));
    }

    [Fact]
    public async Task An_unknown_outcome_is_refused_with_a_field_error()
    {
        var response = await _client.SendAsync(Request($"{ListUrl}?outcome=Maybe", permissions: "audit.view"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("errors").TryGetProperty("outcome", out _));
    }

    [Fact]
    public async Task A_missing_correlation_id_is_created_and_returned()
    {
        var response = await _client.SendAsync(Request(ListUrl, permissions: "audit.view"));

        var id = response.Headers.GetValues(CorrelationKeys.HeaderName).Single();
        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Fact]
    public void Every_endpoint_declares_a_known_permission_or_is_an_explicit_anonymous_health_probe()
    {
        var endpoints = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        Assert.Contains(endpoints, e => e.RoutePattern.RawText == ListUrl);

        foreach (var endpoint in endpoints)
        {
            var route = endpoint.RoutePattern.RawText ?? endpoint.DisplayName ?? "(unnamed)";
            var anonymous = endpoint.Metadata.OfType<IAllowAnonymous>().Any();
            var permissions = endpoint.Metadata.OfType<RequiresPermissionAttribute>().Select(a => a.Permission).ToList();

            if (anonymous)
            {
                Assert.Contains(route, AnonymousAllowList);
            }
            else
            {
                Assert.Single(permissions);
                Assert.True(PermissionCatalogue.IsKnown(permissions[0]), $"{route} declares an unknown permission.");
            }
        }
    }

    private static HttpRequestMessage Request(string url, string? permissions = null, string? correlation = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Test-User", "5b0a1c2d-0000-4000-8000-000000000042");
        if (permissions is not null)
        {
            request.Headers.Add("X-Test-Permissions", permissions);
        }

        if (correlation is not null)
        {
            request.Headers.Add(CorrelationKeys.HeaderName, correlation);
        }

        return request;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
