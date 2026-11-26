namespace DevAgent.Application.Common;

/// <summary>Use case thay đổi trạng thái. Mỗi command có đúng một handler, đăng ký tường minh theo module.</summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Use case chỉ đọc.</summary>
public interface IQueryHandler<in TQuery, TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
