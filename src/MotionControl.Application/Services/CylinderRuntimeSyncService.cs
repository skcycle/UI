using System.Linq;
using MotionControl.Application.Interfaces;
using MotionControl.Domain.Entities;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class CylinderRuntimeSyncService(Machine machine)
    : RuntimeSyncServiceBase<CylinderConfigItem, Cylinder>(machine), ICylinderRuntimeSyncService
{
    public override Task ApplyAsync(CylinderConfigItem cylinder, CancellationToken cancellationToken = default)
    {
        EnsureIoPointExists(cylinder.ExtendSensorInputAddress, isOutput: false);
        EnsureIoPointExists(cylinder.RetractSensorInputAddress, isOutput: false);
        EnsureIoPointExists(cylinder.ExtendOutputAddress, isOutput: true);
        EnsureIoPointExists(cylinder.RetractOutputAddress, isOutput: true);

        var existing = FindExisting(cylinder, c => c.Name);
        if (existing is null)
        {
            Machine.AddCylinder(new Cylinder(
                cylinder.Name, cylinder.ExtendSensorInputAddress, cylinder.RetractSensorInputAddress,
                cylinder.ExtendOutputAddress, cylinder.RetractOutputAddress,
                cylinder.Description, cylinder.ActionTimeoutMs));
        }
        else
        {
            existing.UpdateMetadata(
                cylinder.Name, cylinder.ExtendSensorInputAddress, cylinder.RetractSensorInputAddress,
                cylinder.ExtendOutputAddress, cylinder.RetractOutputAddress,
                cylinder.Description, cylinder.ActionTimeoutMs);
        }
        return Task.CompletedTask;
    }

    protected override IEnumerable<Cylinder> GetAllRuntime() => Machine.Cylinders;

    protected override string GetConfigName(CylinderConfigItem config) => config.Name;

    public override Task RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        Machine.RemoveCylinder(name);
        return Task.CompletedTask;
    }

    protected override void RemoveRuntime(Cylinder item) => Machine.RemoveCylinder(item.Name);
}
