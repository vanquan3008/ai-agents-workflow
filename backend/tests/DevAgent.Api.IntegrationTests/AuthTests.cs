using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DevAgent.Api.IntegrationTests;

/// <summary>End-to-end authentication: sign-in, forced password change, refresh rotation, reuse detection, lockout and administration.</summary>
public sealed class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed class Browser
    {
        private readonly HttpClient _client;
        private string? _refreshCookie;

        public Browser(ApiFactory factory)
        {
            _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        }

        public string? AccessToken { get; private set; }

        public string? RefreshCookie => _refreshCookie;

        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, bool withClientHeader = true, string? cookieOverride = null)
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (AccessToken is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            }

            if (withClientHeader)
            {
                request.Headers.Add("X-DevAgent-Client", "web");
            }

            var cookie = cookieOverride ?? _refreshCookie;
            if (cookie is not null)
            {
                request.Headers.Add("Cookie", $"devagent_rt={cookie}");
            }

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var header in setCookies.Where(h => h.StartsWith("devagent_rt=", StringComparison.Ordinal)))
                {
                    var value = header["devagent_rt=".Length..header.IndexOf(';', StringComparison.Ordinal)];
                    _refreshCookie = value.Length == 0 ? null : value;
                }
            }

            // Buffer the body so the test can read it again after the helper.
            await response.Content.LoadIntoBufferAsync(TestContext.Current.CancellationToken);
            if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("accessToken", out var token))
                {
                    AccessToken = token.GetString();
                }
            }

            return response;
        }

        public Task<HttpResponseMessage> LoginAsync(string email, string password) =>
            SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email, password });
    }

    private async Task ArrangeAsync()
    {
        Assert.SkipUnless(factory.DockerAvailable, "Cần Docker.");
        _ = factory.CreateClient();
        await factory.SeedAsync();
    }

    [Fact]
    public async Task Seeded_super_admin_must_change_password_before_using_api()
    {
        // Bootstrap needs its own database because other admin tests modify the super admin account.
        await using var bootstrapFactory = new ApiFactory();
        await bootstrapFactory.InitializeAsync();
        Assert.SkipUnless(bootstrapFactory.DockerAvailable, "Cần Docker.");
        await bootstrapFactory.SeedAsync();
        var browser = new Browser(bootstrapFactory);

        var login = await browser.LoginAsync(TestData.SuperAdminEmail, TestData.SuperAdminPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        body.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();

        body.GetProperty("user").GetProperty("systemRole").GetString().ShouldBe("SUPER_ADMIN");
        browser.RefreshCookie.ShouldNotBeNull();

        var blocked = await browser.SendAsync(HttpMethod.Get, "/api/v1/projects");
        blocked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await blocked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("auth.password_change_required");

        (await browser.SendAsync(HttpMethod.Get, "/api/v1/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var weak = await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/change-password", new { currentPassword = TestData.SuperAdminPassword, newPassword = "short" });
        weak.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var changed = await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/change-password", new { currentPassword = TestData.SuperAdminPassword, newPassword = "Brand-new-pass-456" });
        changed.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await browser.SendAsync(HttpMethod.Get, "/api/v1/projects")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await browser.SendAsync(HttpMethod.Get, "/api/v1/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_rotates_and_reuse_of_old_token_revokes_the_family()
    {
        await ArrangeAsync();
        var browser = new Browser(factory);
        (await browser.LoginAsync("requester@devagent.test", TestData.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var first = browser.RefreshCookie!;

        (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = browser.RefreshCookie!;
        second.ShouldNotBe(first);

        // An attacker replays the old token: rejected, and the whole token family is revoked.
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookieOverride: first)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookieOverride: second)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_requires_client_header_and_logout_revokes_session()
    {
        await ArrangeAsync();
        var browser = new Browser(factory);
        await browser.LoginAsync("approver@devagent.test", TestData.Password);

        (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", withClientHeader: false)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var cookie = browser.RefreshCookie!;
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/logout")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookieOverride: cookie)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Repeated_wrong_password_locks_account_and_is_audited()
    {
        await ArrangeAsync();
        var browser = new Browser(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<DevAgent.Application.Abstractions.Security.IPasswordHasher>();
            var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
            if (!await db.Users.AnyAsync(u => u.Email == "lockme@devagent.test", TestContext.Current.CancellationToken))
            {
                var user = Domain.Users.User.Create("lockme@devagent.test", "Lock", Domain.Users.SystemRole.User, DateTimeOffset.UtcNow);
                user.SetPassword(hasher.Hash(TestData.Password), false);
                db.Users.Add(user);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        for (var i = 0; i < 5; i++)
        {
            (await browser.LoginAsync("lockme@devagent.test", "wrong-password-1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var locked = await browser.LoginAsync("lockme@devagent.test", TestData.Password);
        locked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await locked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("auth.locked_out");

        using var verify = factory.Services.CreateScope();
        var audit = verify.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        (await audit.AuditEntries.CountAsync(a => a.Action == "auth.login" && a.Outcome == Domain.Auditing.AuditOutcome.Denied, TestContext.Current.CancellationToken))
            .ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_return_same_error()
    {
        await ArrangeAsync();
        var browser = new Browser(factory);

        var unknown = await (await browser.LoginAsync("nobody@devagent.test", "Whatever-123")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var wrong = await (await browser.LoginAsync("owner@devagent.test", "Whatever-123")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        unknown.ShouldContain("auth.invalid_credentials");
        wrong.ShouldContain("auth.invalid_credentials");
    }

    [Fact]
    public async Task Admin_endpoints_are_super_admin_only_and_deactivation_kills_tokens()
    {
        await ArrangeAsync();
        var owner = factory.ClientFor(TestData.OwnerUserId.Value);
        (await owner.GetAsync("/api/v1/admin/users", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await owner.PostAsJsonAsync("/api/v1/projects", new { name = "X", slug = $"x-{Guid.NewGuid():N}"[..12] }, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Whether or not the super admin changed the password, administer with a directly signed token.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevAgentDbContext>();
        var adminId = (await db.Users.SingleAsync(u => u.Email == TestData.SuperAdminEmail, TestContext.Current.CancellationToken)).Id.Value;
        var adminUser = await db.Users.SingleAsync(u => u.Id == new Domain.Common.UserId(adminId), TestContext.Current.CancellationToken);
        if (adminUser.MustChangePassword)
        {
            adminUser.SetPassword(scope.ServiceProvider.GetRequiredService<DevAgent.Application.Abstractions.Security.IPasswordHasher>().Hash("Brand-new-pass-456"), false);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var admin = factory.ClientFor(adminId);
        var email = $"new-{Guid.NewGuid():N}@devagent.test";
        var created = await admin.PostAsJsonAsync("/api/v1/admin/users",
            new { email, displayName = "Người mới", systemRole = "USER", temporaryPassword = "Temp-pass-789" }, TestContext.Current.CancellationToken);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var userId = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();

        var userClient = factory.ClientFor(userId);
        (await userClient.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await admin.PatchAsJsonAsync($"/api/v1/admin/users/{userId}", new { isActive = false }, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await userClient.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var selfDemote = await admin.PatchAsJsonAsync($"/api/v1/admin/users/{adminId}", new { systemRole = "USER" }, TestContext.Current.CancellationToken);
        selfDemote.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The super admin creates a project, assigns an owner by email and sees every project.
        var slug = $"p-{Guid.NewGuid():N}"[..14];
        var project = await admin.PostAsJsonAsync("/api/v1/projects", new { name = "Dự án mới", slug, ownerEmail = "owner@devagent.test" }, TestContext.Current.CancellationToken);
        project.StatusCode.ShouldBe(HttpStatusCode.Created);
        var projects = await admin.GetFromJsonAsync<JsonElement>("/api/v1/projects", TestContext.Current.CancellationToken);
        projects.EnumerateArray().Select(p => p.GetProperty("slug").GetString()).ShouldContain(slug);
        projects.EnumerateArray().Select(p => p.GetProperty("slug").GetString()).ShouldContain(TestData.ProjectSlug);
    }

    [Fact]
    public async Task Owner_manages_members_by_email()
    {
        await ArrangeAsync();
        var owner = factory.ClientFor(TestData.OwnerUserId.Value);
        var projects = await owner.GetFromJsonAsync<JsonElement>("/api/v1/projects", TestContext.Current.CancellationToken);
        var projectId = projects.EnumerateArray().Single(p => p.GetProperty("slug").GetString() == TestData.ProjectSlug).GetProperty("id").GetGuid();

        var missing = await owner.PutAsJsonAsync($"/api/v1/projects/{projectId}/members", new { email = "ghost@devagent.test", role = "VIEWER" }, TestContext.Current.CancellationToken);
        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await owner.PutAsJsonAsync($"/api/v1/projects/{projectId}/members", new { email = "outsider@devagent.test", role = "VIEWER" }, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var members = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{projectId}/members", TestContext.Current.CancellationToken);
        members.EnumerateArray().ShouldContain(m => m.GetProperty("email").GetString() == "outsider@devagent.test");

        (await owner.DeleteAsync($"/api/v1/projects/{projectId}/members/{TestData.OutsiderUserId.Value}", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var lastOwner = await owner.DeleteAsync($"/api/v1/projects/{projectId}/members/{TestData.OwnerUserId.Value}", TestContext.Current.CancellationToken);
        lastOwner.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }
}
