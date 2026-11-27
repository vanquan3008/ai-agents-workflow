namespace DevAgent.Application.Common;

/// <summary>A state-changing use case. Each command has exactly one handler, registered explicitly per module.</summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
