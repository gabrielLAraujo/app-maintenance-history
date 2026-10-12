using Garage.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Garage.Infrastructure.Persistence.Configurations;

internal sealed class CatalogCacheConfiguration : IEntityTypeConfiguration<CatalogCacheEntry>
{
    public void Configure(EntityTypeBuilder<CatalogCacheEntry> builder)
    {
        builder.ToTable("catalog_cache");

        builder.HasKey(e => e.Key);
        builder.Property(e => e.Key).HasColumnType("text");
        builder.Property(e => e.Payload).HasColumnType("jsonb");
        builder.Property(e => e.FetchedAt).HasColumnType("timestamp with time zone");
    }
}
