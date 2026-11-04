using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Edm.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Edm.IntegrationTests;

/// <summary>
/// Local sign-in end to end: the real session cookie, the database-backed accounts, the lockout and the audit trail.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class IdentityApiTests(PostgresServer server) : IAsyncLifetime
{
    private const string Username = "admin.test";
    private const string Password = "correct horse battery staple";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string SignOutUrl = "/api/v1/auth/sign-out";
    private const string SessionUrl = "/api/v1/auth/session";
    private const string AuditUrl = "/api/v1/audit-events";

    private PostgresTestDatabase _db = null!;
    private EdmApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _db = await server.CreateDatabaseAsync();
        _factory = new EdmApiFactory(
            _db.ConnectionString,
            useTestAuthentication: false,
            bootstrap: new BootstrapAccount(Username, Password, "AUDITOR"));

        // Secure cookies are only sent over https, so the test client uses an https base address.
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task A_correct_password_signs_in_and_sets_a_secure_http_only_strict_session_cookie()
    {
        var response = await SignInAsync(Username, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = SessionCookie(response);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_session_returns_the_user_their_roles_and_the_permissions_those_roles_grant()
    {
        await SignInAsync(Username, Password);

        var response = await _client.GetAsync(SessionUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal(Username, body.RootElement.GetProperty("username").GetString());
        Assert.Contains("AUDITOR", Strings(body.RootElement, "roles"));
        Assert.Contains("audit.view", Strings(body.RootElement, "permissions"));
        Assert.DoesNotContain("org.user.manage", Strings(body.RootElement, "permissions"));
    }

    [Fact]
    public async Task Without_a_session_the_session_endpoint_answers_401_with_EDM_AUTH_001()
    {
        var response = await _client.GetAsync(SessionUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("EDM-AUTH-001", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_user_get_the_same_answer()
    {
        var wrong = await SignInAsync(Username, "not the password at all");
        var unknown = await SignInAsync("nobody.here", "not the password at all");

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        using var wrongBody = await ReadJsonAsync(wrong);
        using var unknownBody = await ReadJsonAsync(unknown);
        Assert.Equal("EDM-AUTH-001", wrongBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            wrongBody.RootElement.GetProperty("message").GetString(),
            unknownBody.RootElement.GetProperty("message").GetString());
        Assert.Equal(SessionCookieNames(wrong), SessionCookieNames(unknown));
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_correct_password()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await SignInAsync(Username, "wrong password here");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var locked = await SignInAsync(Username, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        using var body = await ReadJsonAsync(locked);
        Assert.Equal("EDM-AUTH-001", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Sign_in_refuses_a_request_that_is_not_json()
    {
        var content = new StringContent("username=admin.test&password=x", Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await _client.PostAsync(SignInUrl, content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Sign_in_with_a_blank_username_or_password_is_422_with_EDM_VAL_001()
    {
        var response = await SignInAsync("", "");

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("EDM-VAL-001", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("username", out _));
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Sign_out_ends_the_session()
    {
        await SignInAsync(Username, Password);

        var signOut = await _client.PostAsync(SignOutUrl, new StringContent("{}", Encoding.UTF8, "application/json"));
        var session = await _client.GetAsync(SessionUrl);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
    }

    [Fact]
    public async Task Sign_in_and_sign_out_are_visible_in_the_audit_viewer_with_their_outcomes()
    {
        await SignInAsync(Username, Password);
        await SignInAsync(Username, "wrong password here");
        await _client.PostAsync(SignOutUrl, new StringContent("{}", Encoding.UTF8, "application/json"));
        await SignInAsync(Username, Password);

        var viewer = await _client.GetAsync(AuditUrl + "?module=Identity&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, viewer.StatusCode);
        using var body = await ReadJsonAsync(viewer);
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => Text(i, "action") == "SignIn" && Text(i, "outcome") == "Success");
        Assert.Contains(items, i => Text(i, "action") == "SignInFailed" && Text(i, "failureReason") == "Invalid password");
        Assert.Contains(items, i => Text(i, "action") == "SignOut");
        Assert.Contains(items, i => Text(i, "action") == "UserCreated");
    }

    [Fact]
    public async Task The_viewer_is_still_closed_to_a_signed_in_user_without_audit_view()
    {
        // A user cannot be created through the API yet, so this uses a bootstrap account with the Requester role only.
        await using var db = await server.CreateDatabaseAsync();
        using var factory = new EdmApiFactory(
            db.ConnectionString,
            useTestAuthentication: false,
            bootstrap: new BootstrapAccount("requester.one", Password, "REQUESTER"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await client.PostAsJsonAsync(SignInUrl, new { username = "requester.one", password = Password });

        var response = await client.GetAsync(AuditUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("EDM-AUTH-002", body.RootElement.GetProperty("code").GetString());
    }

    private Task<HttpResponseMessage> SignInAsync(string username, string password)
        => _client.PostAsJsonAsync(SignInUrl, new { username, password });

    private static string SessionCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values), "The response set no cookie.");
        return Assert.Single(values!, v => v.StartsWith("edm.session=", StringComparison.Ordinal));
    }

    private static string[] SessionCookieNames(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(v => v.Split('=')[0]).Order(StringComparer.Ordinal).ToArray()
            : [];

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static string[] Strings(JsonElement root, string property)
        => root.GetProperty(property).EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();

    private static string? Text(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
