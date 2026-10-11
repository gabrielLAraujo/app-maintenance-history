using Garage.Application.Sharing;
using Garage.Domain.Sharing;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Garage.Infrastructure.Persistence.Configurations;

internal sealed class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> builder)
    {
        builder.ToTable("share_links");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        // Do agregado, nunca coluna.
        builder.Ignore(l => l.DomainEvents);

        // Guardado em texto para o dono poder copiar o link de novo (spec 10.3); a visão pública o busca pelo índice único.
        builder.Property(l => l.Token).HasMaxLength(ShareTokens.Length);
        builder.HasIndex(l => l.Token).IsUnique();

        builder.Property(l => l.Label).HasMaxLength(ShareLink.MaxLabelLength);

        builder.HasIndex(l => l.VehicleId);
        // O nome é explícito porque a convenção snake_case o monta com o nome da tabela do veículo ainda não configurada
        // (as configurações são aplicadas em ordem alfabética) e sairia "fk_share_links_vehicle_vehicle_id".
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(l => l.VehicleId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_share_links_vehicles_vehicle_id");
    }
}
