using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevAgent.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    /// <summary>
    /// Optimistic concurrency via PostgreSQL's <c>xmin</c> system column: when two processes modify the same aggregate,
    /// the later save gets <see cref="DbUpdateConcurrencyException"/> instead of silently overwriting.
    /// </summary>
    public static void HasXminConcurrencyToken<T>(this EntityTypeBuilder<T> builder)
        where T : class =>
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
}
