using Garage.Domain.Attachments;
using Garage.Domain.Records;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Garage.Infrastructure.Persistence.Configurations;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        // Do agregado, nunca coluna.
        builder.Ignore(a => a.DomainEvents);

        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.FileName).HasMaxLength(Attachment.MaxFileNameLength);
        builder.Property(a => a.ContentType).HasMaxLength(50);
        // "{vehicleId}/{attachmentId}": dois uuids e a barra.
        builder.Property(a => a.StorageKey).HasMaxLength(73);

        // Os nomes são explícitos porque a convenção snake_case os monta com os nomes das tabelas ainda não configuradas
        // (as configurações são aplicadas em ordem alfabética) e sairiam "fk_attachments_service_record_record_id" etc.
        // Excluir o registro ou o veículo apaga as linhas; os arquivos são apagados pelos handlers depois do commit.
        builder.HasIndex(a => a.RecordId);
        builder.HasOne<ServiceRecord>().WithMany().HasForeignKey(a => a.RecordId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_attachments_service_records_record_id");

        builder.HasIndex(a => a.VehicleId);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_attachments_vehicles_vehicle_id");
    }
}
