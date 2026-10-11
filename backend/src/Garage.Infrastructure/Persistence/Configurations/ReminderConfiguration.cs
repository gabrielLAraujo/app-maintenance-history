using Garage.Domain.Common;
using Garage.Domain.Reminders;
using Garage.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Garage.Infrastructure.Persistence.Configurations;

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("reminders");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        // Do agregado, nunca coluna.
        builder.Ignore(r => r.DomainEvents);

        builder.Property(r => r.Title).HasMaxLength(120);

        // Opcional: o conversor do Odometer vale também para a propriedade anulável (o EF não converte nulos).
        builder.Property(r => r.LastDoneOdometer)
            .HasConversion(new ValueConverter<Odometer, int>(odometer => odometer.Km, km => new Odometer(km)));

        // Concorrência otimista: o Npgsql mapeia a propriedade sombra para a coluna de sistema xmin.
        builder.Property<uint>("RowVersion").IsRowVersion();

        builder.HasIndex(r => r.VehicleId);
        // O nome é explícito porque a convenção snake_case o monta com o nome da tabela do veículo ainda não configurada
        // (as configurações são aplicadas em ordem alfabética) e sairia "fk_reminders_vehicle_vehicle_id".
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_reminders_vehicles_vehicle_id");
    }
}
