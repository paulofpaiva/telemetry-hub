using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelemetryHub.Domain.Devices;

namespace TelemetryHub.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(device => device.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(DeviceId.MaxLength)
            .IsRequired();
        builder.HasIndex(device => device.DeviceId).IsUnique();

        builder.Property(device => device.Name)
            .HasColumnName("name")
            .HasMaxLength(Device.NameMaxLength)
            .IsRequired();

        builder.Property(device => device.Location)
            .HasColumnName("location")
            .HasMaxLength(Device.LocationMaxLength);

        builder.Property(device => device.IsActive).HasColumnName("is_active");
        builder.Property(device => device.CreatedAt).HasColumnName("created_at");
        builder.Property(device => device.UpdatedAt).HasColumnName("updated_at");
    }
}
