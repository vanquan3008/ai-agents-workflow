using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Domain.Common;
using DevAgent.Domain.Departments;
using Microsoft.Extensions.Caching.Memory;

namespace DevAgent.Infrastructure.Persistence.Queries;

/// <summary>
/// In-process cache of published revision snapshots. Revisions never change after publication, so entries need no
/// invalidation; a new publication simply has a new key. A dedicated, size-bounded cache keeps department entries from
/// competing with other cache users. Keys include the project so a lookup stays project-scoped like the database query.
/// Authorization and the published pointer are never cached.
/// </summary>
internal sealed class DepartmentRevisionCache : IDisposable
{
    private const int MaxEntries = 1024;
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = MaxEntries });

    public bool TryGet(ProjectId projectId, ItDepartmentId departmentId, int version, out DepartmentRevisionSnapshot? snapshot) =>
        _cache.TryGetValue((projectId, departmentId, version), out snapshot);

    public void Set(ProjectId projectId, DepartmentRevisionSnapshot snapshot) =>
        _cache.Set((projectId, snapshot.DepartmentId, snapshot.Version), snapshot, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = SlidingExpiration,
        });

    public void Dispose() => _cache.Dispose();
}
