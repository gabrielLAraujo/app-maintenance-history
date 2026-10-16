using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Garage.Domain.Vehicles;
using Garage.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Garage.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        // Calculados ou do agregado, nunca colunas.
        builder.Ignore(v => v.DisplayName);
        builder.Ignore(v => v.DomainEvents);

        builder.Property(v => v.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.Make).HasMaxLength(60);
        builder.Property(v => v.Model).HasMaxLength(60);
        builder.Property(v => v.Version).HasMaxLength(120);
        builder.Property(v => v.Generation).HasMaxLength(30);
        builder.Property(v => v.Fuel).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.Nickname).HasMaxLength(60);
        builder.Property(v => v.Vin).HasMaxLength(17);
        builder.Property(v => v.Notes).HasMaxLength(2000);

        builder.ComplexProperty(v => v.Year, year =>
        {
            year.Property(y => y.Manufacture).HasColumnName("manufacture_year");
            year.Property(y => y.Model).HasColumnName("model_year");
        });

        builder.Property(v => v.Plate)
            .HasConversion(plate => plate!.Value, value => Plate.From(value))
            .HasMaxLength(7);

        builder.Property(v => v.CurrentOdometer)
            .HasConversion(odometer => odometer.Km, km => new Odometer(km));

        // Opcional: sem vínculo com a FIPE as três colunas ficam nulas.
        builder.OwnsOne(v => v.Catalog, catalog =>
        {
            catalog.Property(c => c.Type).HasColumnName("catalog_type").HasConversion<string>().HasMaxLength(20);
            catalog.Property(c => c.FipeCode).HasColumnName("catalog_fipe_code").HasMaxLength(20);
            catalog.Property(c => c.FipeYearCode).HasColumnName("catalog_fipe_year_code").HasMaxLength(20);
        });

        // Concorrência otimista: o Npgsql mapeia a propriedade sombra para a coluna de sistema xmin.
        builder.Property<uint>("RowVersion").IsRowVersion();

        builder.HasIndex(v => v.OwnerId);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(v => v.OwnerId).OnDelete(DeleteBehavior.Cascade);

        // C1: a capa é um anexo do próprio veículo; apagar o anexo limpa a capa.
        builder.HasOne<Attachment>().WithMany().HasForeignKey(v => v.CoverPhotoId).OnDelete(DeleteBehavior.SetNull);
    }
}
