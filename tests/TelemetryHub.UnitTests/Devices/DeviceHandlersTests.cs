using TelemetryHub.Application.Devices.CreateDevice;
using TelemetryHub.Application.Devices.DeleteDevice;
using TelemetryHub.Application.Devices.GetDevice;
using TelemetryHub.Application.Devices.ListDevices;
using TelemetryHub.Application.Devices.UpdateDevice;
using TelemetryHub.Application.Exceptions;
using TelemetryHub.Domain.Devices;
using TelemetryHub.UnitTests.Fakes;

namespace TelemetryHub.UnitTests.Devices;

public class DeviceHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDeviceRepository _repository = new();
    private readonly FixedTimeProvider _clock = new(Now);

    [Fact]
    public async Task Create_stores_an_active_device_and_returns_it()
    {
        var handler = new CreateDeviceCommandHandler(_repository, _clock);

        var created = await handler.Handle(new CreateDeviceCommand("pump-001", "  Feed pump  ", " Unit A "), CancellationToken.None);

        var stored = Assert.Single(_repository.Devices);
        Assert.Equal(stored.Id, created.Id);
        Assert.Equal("Feed pump", created.Name);
        Assert.Equal("Unit A", created.Location);
        Assert.True(created.IsActive);
        Assert.Equal(Now, created.CreatedAt);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Create_fails_with_conflict_when_the_device_id_is_taken()
    {
        _repository.Add(Device.Create("pump-001", "Existing", null, Now));
        var handler = new CreateDeviceCommandHandler(_repository, _clock);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CreateDeviceCommand("pump-001", "Another", null), CancellationToken.None));

        Assert.Single(_repository.Devices);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Theory]
    [InlineData("Pump 1", "Name")]
    [InlineData("pump-001", "")]
    public void Create_validator_rejects_bad_input(string deviceId, string name)
    {
        var result = new CreateDeviceCommandValidator().Validate(new CreateDeviceCommand(deviceId, name, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Update_changes_fields_and_bumps_updated_at_only()
    {
        _repository.Add(Device.Create("pump-001", "Old name", "Unit A", Now));
        _clock.Now = Now.AddHours(1);
        var handler = new UpdateDeviceCommandHandler(_repository, _clock);

        var updated = await handler.Handle(new UpdateDeviceCommand("pump-001", "New name", null, false), CancellationToken.None);

        Assert.Equal("New name", updated.Name);
        Assert.Null(updated.Location);
        Assert.False(updated.IsActive);
        Assert.Equal(Now, updated.CreatedAt);
        Assert.Equal(Now.AddHours(1), updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_get_and_delete_fail_with_not_found_for_unknown_devices()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateDeviceCommandHandler(_repository, _clock)
            .Handle(new UpdateDeviceCommand("ghost", "Name", null, true), CancellationToken.None));

        await Assert.ThrowsAsync<NotFoundException>(() => new GetDeviceQueryHandler(_repository)
            .Handle(new GetDeviceQuery("ghost"), CancellationToken.None));

        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteDeviceCommandHandler(_repository)
            .Handle(new DeleteDeviceCommand("ghost"), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_removes_the_device()
    {
        _repository.Add(Device.Create("pump-001", "Feed pump", null, Now));

        await new DeleteDeviceCommandHandler(_repository).Handle(new DeleteDeviceCommand("pump-001"), CancellationToken.None);

        Assert.Empty(_repository.Devices);
    }

    [Fact]
    public async Task List_pages_and_filters_by_active_flag()
    {
        for (var index = 1; index <= 5; index++)
        {
            var device = Device.Create($"pump-00{index}", $"Pump {index}", null, Now);
            if (index % 2 == 0)
            {
                device.Update(device.Name, null, isActive: false, Now);
            }

            _repository.Add(device);
        }

        var handler = new ListDevicesQueryHandler(_repository);

        var secondPage = await handler.Handle(new ListDevicesQuery(Page: 2, PageSize: 2), CancellationToken.None);
        var inactive = await handler.Handle(new ListDevicesQuery(IsActive: false), CancellationToken.None);

        Assert.Equal(5, secondPage.TotalCount);
        Assert.Equal(["pump-003", "pump-004"], secondPage.Items.Select(item => item.DeviceId));
        Assert.Equal(["pump-002", "pump-004"], inactive.Items.Select(item => item.DeviceId));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void List_validator_rejects_out_of_range_paging(int page, int pageSize)
    {
        Assert.False(new ListDevicesQueryValidator().Validate(new ListDevicesQuery(page, pageSize)).IsValid);
    }
}
