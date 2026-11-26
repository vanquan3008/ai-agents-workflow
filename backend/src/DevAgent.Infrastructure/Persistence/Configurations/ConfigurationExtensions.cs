using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevAgent.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    /// <summary>
    /// Optimistic concurrency bằng cột hệ thống <c>xmin</c> của PostgreSQL: hai tiến trình sửa cùng aggregate
    /// thì tiến trình lưu sau nhận <see cref="DbUpdateConcurrencyException"/> thay vì ghi đè im lặng.
    /// </summary>
    public static void HasXminConcurrencyToken<T>(this EntityTypeBuilder<T> builder)
        where T : class =>
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
}
