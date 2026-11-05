using System.Net;
using System.Text;
using System.Text.Json;
using Edm.IntegrationTests.Support;

namespace Edm.IntegrationTests;

/// <summary>Department-head limits over HTTP, against PostgreSQL: one active row, history, concurrency, rules and audit.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DeptHeadLimitApiTests(PostgresServer server) : IAsyncLifetime
{
    private const string Admin = "config.limit.manage,audit.view";
    private const string Url = "/api/v1/dept-head-limits";

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

    /// <summary>Today in Karachi, as the server computes it.</summary>
    private static DateOnly KarachiToday() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    [Fact]
    public async Task With_no_limit_the_summary_shows_the_seeded_default()
    {
        var response = await Send(HttpMethod.Get, $"{Url}?nature=OPEX", Admin);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("OPEX", response.Body.GetProperty("nature").GetString());
        Assert.Equal(20000m, response.Body.GetProperty("currentAmountPkr").GetDecimal());
        Assert.True(response.Body.GetProperty("isDefault").GetBoolean());
        Assert.Equal("Default", response.Body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, response.Body.GetProperty("active").ValueKind);
        Assert.Empty(response.Body.GetProperty("history").EnumerateArray());
    }

    [Fact]
    public async Task A_limit_dated_today_applies_at_once()
    {
        var created = await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 50000.50m, description = "  Department heads  ", effectiveDate = KarachiToday(), rowVersion = (long?)null });

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(50000.50m, created.Body.GetProperty("amountPkr").GetDecimal());
        Assert.Equal("Department heads", created.Body.GetProperty("description").GetString());
        Assert.True(created.Body.GetProperty("isActive").GetBoolean());
        Assert.Equal(1, created.Body.GetProperty("rowVersion").GetInt64());

        var summary = await Send(HttpMethod.Get, $"{Url}?nature=OPEX", Admin);
        Assert.False(summary.Body.GetProperty("isDefault").GetBoolean());
        Assert.Equal(50000.50m, summary.Body.GetProperty("currentAmountPkr").GetDecimal());
        Assert.Equal("Active", summary.Body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_limit_dated_tomorrow_is_scheduled_and_the_default_still_applies()
    {
        await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 50000m, description = (string?)null, effectiveDate = KarachiToday().AddDays(1), rowVersion = (long?)null });

        var summary = await Send(HttpMethod.Get, $"{Url}?nature=OPEX", Admin);

        Assert.Equal("Scheduled", summary.Body.GetProperty("status").GetString());
        Assert.Equal(20000m, summary.Body.GetProperty("currentAmountPkr").GetDecimal());
    }

    [Fact]
    public async Task Changing_the_limit_keeps_the_old_one_as_history()
    {
        var first = await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 50000m, description = (string?)null, effectiveDate = KarachiToday(), rowVersion = (long?)null });
        var firstVersion = first.Body.GetProperty("rowVersion").GetInt64();

        var second = await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 75000m, description = (string?)null, effectiveDate = KarachiToday().AddDays(1), rowVersion = (long?)firstVersion });

        Assert.Equal(HttpStatusCode.Created, second.Status);
        var summary = await Send(HttpMethod.Get, $"{Url}?nature=OPEX", Admin);
        Assert.Equal(75000m, summary.Body.GetProperty("active").GetProperty("amountPkr").GetDecimal());
        var history = summary.Body.GetProperty("history").EnumerateArray().ToArray();
        Assert.Equal(2, history.Length);
        var ended = history.Single(h => !h.GetProperty("isActive").GetBoolean());
        Assert.Equal(50000m, ended.GetProperty("amountPkr").GetDecimal());
        Assert.False(ended.GetProperty("effectiveTo").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task A_save_over_a_changed_limit_is_refused_with_412()
    {
        var first = await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 50000m, description = (string?)null, effectiveDate = KarachiToday(), rowVersion = (long?)null });
        var firstVersion = first.Body.GetProperty("rowVersion").GetInt64();
        await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 60000m, description = (string?)null, effectiveDate = KarachiToday().AddDays(1), rowVersion = (long?)firstVersion });

        var stale = await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 90000m, description = (string?)null, effectiveDate = KarachiToday().AddDays(2), rowVersion = (long?)firstVersion });

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.Status);
        Assert.Equal("EDM-CONC-001", stale.Body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Zero_and_three_decimal_amounts_are_field_errors_on_the_amount()
    {
        var zero = await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 0m, description = (string?)null, effectiveDate = KarachiToday(), rowVersion = (long?)null });
        var threeDecimals = await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 50000.125m, description = (string?)null, effectiveDate = KarachiToday(), rowVersion = (long?)null });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, zero.Status);
        Assert.Equal("EDM-VAL-001", zero.Body.GetProperty("code").GetString());
        Assert.True(zero.Body.GetProperty("errors").TryGetProperty("amountPkr", out _));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, threeDecimals.Status);
        Assert.True(threeDecimals.Body.GetProperty("errors").TryGetProperty("amountPkr", out _));
    }

    [Fact]
    public async Task A_past_effective_date_is_a_field_error_on_the_date()
    {
        var response = await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 50000m, description = (string?)null, effectiveDate = KarachiToday().AddDays(-1), rowVersion = (long?)null });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.True(response.Body.GetProperty("errors").TryGetProperty("effectiveDate", out _));
    }

    [Fact]
    public async Task An_unknown_nature_is_refused()
    {
        var response = await Send(HttpMethod.Get, $"{Url}?nature=PAYROLL", Admin);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.True(response.Body.GetProperty("errors").TryGetProperty("nature", out _));
    }

    [Fact]
    public async Task Deactivating_ends_the_limit_and_a_second_deactivate_finds_nothing()
    {
        var created = await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 50000m, description = (string?)null, effectiveDate = KarachiToday(), rowVersion = (long?)null });
        var version = created.Body.GetProperty("rowVersion").GetInt64();

        var deactivated = await Send(HttpMethod.Post, $"{Url}/deactivate", Admin, new { nature = "OPEX", rowVersion = version });
        var again = await Send(HttpMethod.Post, $"{Url}/deactivate", Admin, new { nature = "OPEX", rowVersion = version });

        Assert.Equal(HttpStatusCode.NoContent, deactivated.Status);
        Assert.Equal(HttpStatusCode.NotFound, again.Status);
        var summary = await Send(HttpMethod.Get, $"{Url}?nature=OPEX", Admin);
        Assert.True(summary.Body.GetProperty("isDefault").GetBoolean());
        Assert.Equal(20000m, summary.Body.GetProperty("currentAmountPkr").GetDecimal());
    }

    [Fact]
    public async Task OPEX_and_CAPEX_limits_do_not_affect_each_other()
    {
        await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 50000m, description = (string?)null, effectiveDate = KarachiToday(), rowVersion = (long?)null });

        var capex = await Send(HttpMethod.Get, $"{Url}?nature=CAPEX", Admin);

        Assert.True(capex.Body.GetProperty("isDefault").GetBoolean());
        Assert.Empty(capex.Body.GetProperty("history").EnumerateArray());
    }

    [Fact]
    public async Task Each_change_is_in_the_audit_viewer_with_its_values()
    {
        var first = await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 50000m, description = (string?)null, effectiveDate = KarachiToday(), rowVersion = (long?)null });
        await Send(HttpMethod.Post, Url, Admin,
            new { nature = "OPEX", amountPkr = 75000m, description = (string?)null, effectiveDate = KarachiToday().AddDays(1), rowVersion = (long?)first.Body.GetProperty("rowVersion").GetInt64() });

        var audit = await Send(HttpMethod.Get, "/api/v1/audit-events?module=Configuration&entityType=DeptHeadLimit", Admin);

        Assert.Equal(HttpStatusCode.OK, audit.Status);
        var actions = audit.Body.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("action").GetString())
            .ToArray();
        Assert.Contains("Created", actions);
        Assert.Contains("Replaced", actions);
    }

    [Fact]
    public async Task A_user_without_config_limit_manage_is_refused()
    {
        var response = await Send(HttpMethod.Get, Url + "?nature=OPEX", "config.category.manage");

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Equal("EDM-AUTH-002", response.Body.GetProperty("code").GetString());
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
