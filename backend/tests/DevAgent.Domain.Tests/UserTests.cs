using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using Shouldly;

namespace DevAgent.Domain.Tests;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Email_is_normalized_and_validated()
    {
        User.Create("Admin@DevAgent.Local", "A", SystemRole.SuperAdmin, Now).Email.ShouldBe("admin@devagent.local");
        Should.Throw<DomainException>(() => User.Create("not-an-email", "A", SystemRole.User, Now)).Code.ShouldBe("user.email_invalid");
    }

    [Fact]
    public void Lockout_after_max_failed_attempts_then_success_clears_it()
    {
        var user = User.Create("u@x.io", "U", SystemRole.User, Now);
        for (var i = 0; i < 4; i++)
        {
            user.RecordFailedLogin(Now, maxAttempts: 5, TimeSpan.FromMinutes(15));
        }

        user.IsLockedOut(Now).ShouldBeFalse();
        user.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
        user.IsLockedOut(Now.AddMinutes(14)).ShouldBeTrue();
        user.IsLockedOut(Now.AddMinutes(16)).ShouldBeFalse();

        user.RecordSuccessfulLogin(Now.AddMinutes(16));
        user.LockoutEndsAt.ShouldBeNull();
        user.FailedLoginCount.ShouldBe(0);
    }

    [Fact]
    public void Security_sensitive_changes_rotate_stamp()
    {
        var user = User.Create("u@x.io", "U", SystemRole.User, Now);
        var stamps = new HashSet<string> { user.SecurityStamp };

        user.SetPassword("hash-1", mustChange: true, Now);
        stamps.Add(user.SecurityStamp).ShouldBeTrue();
        user.ChangeRole(SystemRole.SuperAdmin, Now);
        stamps.Add(user.SecurityStamp).ShouldBeTrue();
        user.Deactivate(Now);
        stamps.Add(user.SecurityStamp).ShouldBeTrue();

        // Every stamp change also announces that open sessions, including realtime connections, must end.
        user.DomainEvents.OfType<UserSessionsInvalidated>().Count().ShouldBe(3);
        user.DomainEvents.OfType<UserSessionsInvalidated>().ShouldAllBe(e => e.UserId == user.Id);

        var before = user.SecurityStamp;
        user.UpgradePasswordHash("hash-2"); // re-hashing the same password keeps sessions valid
        user.SecurityStamp.ShouldBe(before);
        user.MustChangePassword.ShouldBeTrue();
    }

    [Fact]
    public void Refresh_token_rotation_and_revocation()
    {
        var token = RefreshToken.Issue(UserId.New(), "h1", null, Now, TimeSpan.FromDays(7), "127.0.0.1", "ua");
        token.IsActive(Now).ShouldBeTrue();
        token.IsActive(Now.AddDays(8)).ShouldBeFalse();

        var next = RefreshToken.Issue(token.UserId, "h2", token.FamilyId, Now, TimeSpan.FromDays(7), null, null);
        token.RotateTo(next.Id, Now);

        token.WasRotated.ShouldBeTrue();
        token.IsActive(Now).ShouldBeFalse();
        next.FamilyId.ShouldBe(token.FamilyId);
        Should.Throw<DomainException>(() => token.RotateTo(RefreshTokenId.New(), Now));

        // Presented again moments later: a parallel refresh. Long after: possible theft.
        token.WasRotatedSince(Now.AddSeconds(-30)).ShouldBeTrue();
        token.WasRotatedSince(Now.AddSeconds(1)).ShouldBeFalse();
        next.WasRotatedSince(Now.AddSeconds(-30)).ShouldBeFalse();

        // Only the client the token was issued to counts as the same browser; an unknown user agent never matches.
        token.WasIssuedTo("ua").ShouldBeTrue();
        token.WasIssuedTo(" ua ").ShouldBeTrue();
        token.WasIssuedTo("other").ShouldBeFalse();
        token.WasIssuedTo(null).ShouldBeFalse();
        next.WasIssuedTo(null).ShouldBeFalse();
    }
}
