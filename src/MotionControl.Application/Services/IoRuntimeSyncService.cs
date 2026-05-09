using System.Linq;
using MotionControl.Application.Interfaces;
using MotionControl.Domain.Entities;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class IoRuntimeSyncService(Machine machine) : IIoRuntimeSyncService
{
    public Task ApplyAsync(IoPointConfigItem ioPoint, CancellationToken cancellationToken = default)
    {
        var existing = machine.IoPoints.FirstOrDefault(item => item.IsOutput == ioPoint.IsOutput && item.Address == ioPoint.Address);
        if (existing is null)
        {
            machine.AddIoPoint(new IoPoint(ioPoint.Name, ioPoint.Address, ioPoint.IsOutput, ioPoint.Description));
        }
        else
        {
            existing.UpdateMetadata(ioPoint.Name, ioPoint.Address, ioPoint.Description);
        }
        return Task.CompletedTask;
    }

    public Task ReloadAsync(IEnumerable<IoPointConfigItem> ioPoints, CancellationToken cancellationToken = default)
    {
        foreach (var ioPoint in machine.IoPoints.ToList())
        {
            machine.RemoveIoPoint(ioPoint.IsOutput, ioPoint.Address);
        }
        foreach (var ioPoint in ioPoints)
        {
            machine.AddIoPoint(new IoPoint(ioPoint.Name, ioPoint.Address, ioPoint.IsOutput, ioPoint.Description));
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(bool isOutput, int address, CancellationToken cancellationToken = default)
    {
        machine.RemoveIoPoint(isOutput, address);
        return Task.CompletedTask;
    }
}
