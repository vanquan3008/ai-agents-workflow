using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks;
using DevAgent.Application.Tests.Fakes;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevAgent.Application.Tests;

public sealed class CreateTaskHandlerTests
{
    private readonly TestFixture _fx = new();

    private CreateTaskHandler Handler() => new(
        _fx.Access, _fx.Store, _fx.Store, _fx.Store, _fx.Store, _fx.Store, _fx.Clock, NullLogger<CreateTaskHandler>.Instance);

    private CreateTaskCommand Command(string key = "dash-001") =>
        new(_fx.Repository.Id.Value, "Bộ lọc sai trạng thái", "Lọc DONE trả về IN_PROGRESS", TaskChannel.Dashboard, null, key);

    [Fact]
    public async Task Creates_task_and_enqueues_processing_job_in_same_unit_of_work()
    {
        var result = await Handler().HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeTrue();
        result.Value.Status.ShouldBe("PENDING");
        _fx.Store.Tasks.ShouldHaveSingleItem();
        _fx.Store.Jobs.ShouldHaveSingleItem().Type.ShouldBe(JobTypes.ProcessTask);
        _fx.Store.Audit.ShouldContain(a => a.Action == "task.create" && a.Outcome == AuditOutcome.Allowed);
        _fx.Store.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_existing_task()
    {
        var first = await Handler().HandleAsync(Command(), CancellationToken.None);
        var second = await Handler().HandleAsync(Command(), CancellationToken.None);

        second.Value.Created.ShouldBeFalse();
        second.Value.TaskId.ShouldBe(first.Value.TaskId);
        _fx.Store.Tasks.Count.ShouldBe(1);
        _fx.Store.Jobs.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Outsider_is_forbidden_and_denial_is_audited()
    {
        _fx.ActAs(_fx.Outsider);

        var result = await Handler().HandleAsync(Command(), CancellationToken.None);

        result.Error!.Type.ShouldBe(ErrorType.Forbidden);
        _fx.Store.Tasks.ShouldBeEmpty();
        _fx.Store.Audit.ShouldContain(a => a.Outcome == AuditOutcome.Denied && a.Reason!.Contains("không thuộc project", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Anonymous_is_unauthorized()
    {
        _fx.ActAsNobody();

        var result = await Handler().HandleAsync(Command(), CancellationToken.None);

        result.Error!.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Missing_idempotency_key_is_validation_error()
    {
        var result = await Handler().HandleAsync(Command(key: ""), CancellationToken.None);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.Details!.ShouldContainKey("idempotencyKey");
    }
}
