using System.Net;
using System.Text.Json;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Infrastructure.Integrations;
using DevAgent.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace DevAgent.Infrastructure.Tests;

public sealed class DiscordChatNotifierTests
{
    private static readonly ChatNotification Notification = new(
        ChatNotificationKind.Completed, Guid.NewGuid(), Guid.NewGuid(), "Bộ lọc sai", "Đã mở PR #7",
        "https://github.com/acme/app/pull/7", null, "notification.send:abc:7");

    private static (DiscordChatNotifier Notifier, StubHandler Handler) Create(Func<HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://discord.test/api/v10/") };
        var options = MsOptions.Create(new DiscordOptions { Enabled = true, BotToken = "t", NotificationChannelId = "123" });
        return (new DiscordChatNotifier(http, options, NullLogger<DiscordChatNotifier>.Instance), handler);
    }

    [Fact]
    public async Task Sends_message_with_stable_nonce_and_no_mentions()
    {
        var (notifier, handler) = Create(() => new HttpResponseMessage(HttpStatusCode.OK));

        await notifier.NotifyAsync(Notification, TestContext.Current.CancellationToken);
        await notifier.NotifyAsync(Notification, TestContext.Current.CancellationToken);

        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0].Path.ShouldBe("/api/v10/channels/123/messages");
        using var first = JsonDocument.Parse(handler.Requests[0].Body);
        using var second = JsonDocument.Parse(handler.Requests[1].Body);
        var nonce = first.RootElement.GetProperty("nonce").GetString()!;
        nonce.Length.ShouldBe(25);
        nonce.ShouldBe(second.RootElement.GetProperty("nonce").GetString());
        first.RootElement.GetProperty("enforce_nonce").GetBoolean().ShouldBeTrue();
        first.RootElement.GetProperty("content").GetString()!.ShouldContain("pull/7");
    }

    [Fact]
    public async Task Rate_limit_becomes_retryable_with_retry_after_from_body()
    {
        var (notifier, _) = Create(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"message\":\"You are being rate limited.\",\"retry_after\":3.5,\"global\":false}"),
        });

        var ex = await Should.ThrowAsync<RetryableJobException>(() => notifier.NotifyAsync(Notification, TestContext.Current.CancellationToken));

        ex.RetryAfter.ShouldBe(TimeSpan.FromSeconds(3.5));
    }

    [Fact]
    public async Task Server_error_is_retryable_and_forbidden_is_permanent()
    {
        var (server, _) = Create(() => new HttpResponseMessage(HttpStatusCode.BadGateway));
        await Should.ThrowAsync<RetryableJobException>(() => server.NotifyAsync(Notification, TestContext.Current.CancellationToken));

        var (forbidden, _) = Create(() => new HttpResponseMessage(HttpStatusCode.Forbidden));
        await Should.ThrowAsync<PermanentJobException>(() => forbidden.NotifyAsync(Notification, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Network_failure_is_retryable()
    {
        var (notifier, _) = Create(() => throw new HttpRequestException("DNS"));

        await Should.ThrowAsync<RetryableJobException>(() => notifier.NotifyAsync(Notification, TestContext.Current.CancellationToken));
    }

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Path, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.AbsolutePath, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return respond();
        }
    }
}
