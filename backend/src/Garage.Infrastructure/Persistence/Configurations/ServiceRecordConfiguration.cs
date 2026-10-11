using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Garage.Infrastructure.Persistence.Configurations;

internal sealed class ServiceRecordConfiguration : IEntityTypeConfiguration<ServiceRecord>
{
    private const string MoneyColumnType = "numeric(12,2)";

    // Opcionais: os conversores valem também para as propriedades anuláveis (o EF não converte nulos).
    private static readonly ValueConverter<Money, decimal> MoneyConverter = new(money => money.Amount, amount => new Money(amount));
    private static readonly ValueConverter<Odometer, int> OdometerConverter = new(odometer => odometer.Km, km => new Odometer(km));

    public void Configure(EntityTypeBuilder<ServiceRecord> builder)
    {
        builder.ToTable("service_records");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        // Calculados ou do agregado, nunca colunas.
        builder.Ignore(r => r.TotalCost);
        builder.Ignore(r => r.DomainEvents);

        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Title).HasMaxLength(120);
        builder.Property(r => r.Description).HasMaxLength(4000);
        builder.Property(r => r.PerformedBy).HasMaxLength(120);
        builder.Property(r => r.ReplacedOriginalPart).HasMaxLength(500);
        builder.Property(r => r.Odometer).HasConversion(OdometerConverter);
        builder.Property(r => r.LaborCost).HasConversion(MoneyConverter).HasColumnType(MoneyColumnType);

        // As peças são do agregado: tabela própria, apagadas com o registro e substituídas inteiras na edição.
        builder.OwnsMany(r => r.Parts, parts =>
        {
            parts.ToTable("service_record_parts");
            parts.WithOwner().HasForeignKey("ServiceRecordId")
                .HasConstraintName("fk_service_record_parts_service_records_service_record_id");

            parts.HasKey(p => p.Id);
            parts.Property(p => p.Id).ValueGeneratedNever();
            parts.Ignore(p => p.LineTotal);

            parts.Property(p => p.Name).HasMaxLength(120);
            parts.Property(p => p.Brand).HasMaxLength(60);
            parts.Property(p => p.PartNumber).HasMaxLength(60);
            parts.Property(p => p.Quantity).HasColumnType("numeric(8,3)");
            parts.Property(p => p.Unit).HasConversion<string>().HasMaxLength(20);
            parts.Property(p => p.UnitPrice).HasConversion(MoneyConverter).HasColumnType(MoneyColumnType);

            // Ordem informada pelo usuário: Part.Position é interna ao domínio, então é mapeada pelo nome. As leituras
            // ordenam por ela (EF.Property<int>(p, "Position")).
            parts.Property<int>("Position");
        });

        // Concorrência otimista: o Npgsql mapeia a propriedade sombra para a coluna de sistema xmin.
        builder.Property<uint>("RowVersion").IsRowVersion();

        // Serve à lista (date DESC, id DESC), aos limites de R3 e à FK do veículo.
        builder.HasIndex(r => new { r.VehicleId, r.Date, r.Id }).IsDescending(false, true, true);
        // O nome é explícito porque a convenção snake_case o monta com o nome da tabela do veículo ainda não configurada
        // (as configurações são aplicadas em ordem alfabética) e sairia "fk_service_records_vehicle_vehicle_id".
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_service_records_vehicles_vehicle_id");
    }
}
