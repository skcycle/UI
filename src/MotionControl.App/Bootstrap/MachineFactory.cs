using MotionControl.Domain.Entities;
using MotionControl.Domain.ValueObjects;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.App.Bootstrap;

public static class MachineFactory
{
    public static Machine CreateDefaultMachine(
        AxisMappingOptions axisMappingOptions,
        IReadOnlyCollection<MotionControl.Infrastructure.Configuration.IoPointConfigItem>? ioPointConfigs = null,
        IReadOnlyCollection<MotionControl.Infrastructure.Configuration.CylinderConfigItem>? cylinderConfigs = null,
        IReadOnlyCollection<MotionControl.Infrastructure.Configuration.WorkHeadConfigItem>? workHeadConfigs = null,
        IReadOnlyCollection<MotionControl.Infrastructure.Configuration.MagazineConfigItem>? magazineConfigs = null)
    {
        var axisNames = axisMappingOptions.AxisNames;
        var axisMappings = axisMappingOptions.Axes;
        var configuredAxisCount = axisMappings.Count > 0
            ? axisMappings.Max(item => item.AxisNo) + 1
            : axisNames.Count;
        var axisCount = Math.Max(1, configuredAxisCount);
        var axes = Enumerable.Range(0, axisCount)
            .Select(axisNo =>
            {
                var mapping = axisMappings.FirstOrDefault(item => item.AxisNo == axisNo);
                var axis = new Axis(
                    new AxisId(axisNo),
                    mapping?.Name ?? (axisNames.Count > axisNo ? axisNames[axisNo] : $"Axis {axisNo}"),
                    axisNo);

                if (mapping?.SoftLimitNegative is not null && mapping.SoftLimitPositive is not null)
                {
                    axis.SetSoftLimit(new SoftLimit(mapping.SoftLimitNegative.Value, mapping.SoftLimitPositive.Value));
                }

                if (mapping is not null)
                {
                    axis.SetHomeMode(mapping.HomeMode);
                    axis.SetServoBinding(mapping.ServoBinding);
                    if (mapping.WorkVelocity.HasValue) axis.SetWorkVelocity(mapping.WorkVelocity.Value);
                    if (mapping.SetupVelocity.HasValue) axis.SetSetupVelocity(mapping.SetupVelocity.Value);
                    if (mapping.PulseEquivalent.HasValue) axis.SetPulseEquivalent(mapping.PulseEquivalent.Value);
                }

                return axis;
            })
            .ToArray();

        var ioPoints = (ioPointConfigs ?? Array.Empty<MotionControl.Infrastructure.Configuration.IoPointConfigItem>())
            .Select(item => new IoPoint(item.Name, item.Address, item.IsOutput, item.Description))
            .ToArray();

        var cylinders = (cylinderConfigs ?? Array.Empty<MotionControl.Infrastructure.Configuration.CylinderConfigItem>())
            .Select(item => new Cylinder(item.Name, item.ExtendSensorInputAddress, item.RetractSensorInputAddress, item.ExtendOutputAddress, item.RetractOutputAddress, item.Description, item.ActionTimeoutMs))
            .ToArray();

        var workHeads = (workHeadConfigs ?? Array.Empty<MotionControl.Infrastructure.Configuration.WorkHeadConfigItem>())
            .Select(item => new WorkHead(item.Name, item.Description, item.XAxisNo, item.YAxisNo, item.ZAxisNo, item.RAxisNo, item.VacuumOutputAddress, item.BlowOutputAddress, item.VacuumInputAddress, item.GeneralOutputAddress1, item.GeneralOutputAddress2, item.GeneralInputAddress1, item.GeneralInputAddress2, item.VacuumTimeoutMs,
                item.Positions.Select(p => new WorkHeadPosition(p.Name, p.Description, p.X, p.Y, p.Z, p.R)).ToList(), item.SafeZ))
            .ToArray();

        var magazines = (magazineConfigs ?? Array.Empty<MotionControl.Infrastructure.Configuration.MagazineConfigItem>())
            .Select(item => new Magazine(item.Name, item.Description, item.XAxisNo, item.YAxisNo, item.ZAxisNo, item.MaterialPresentInputAddress, item.CurrentLayerHasMaterialInputAddress, item.TrayKeyingInputAddress, item.LayerCount, item.LayerHeight, item.PickLiftHeight, item.ScanSettlingMs))
            .ToArray();

        var alarms = new[]
        {
            new Alarm("SYS-001", "EtherCAT controller not connected", DateTime.Now, "System", "Communication", "Warning"),
            new Alarm("AXIS-001", "Axis 1 follow error placeholder", DateTime.Now, "Axis 1", "Motion", "Error")
        };

        alarms[0].Clear();
        alarms[1].Clear();

        return new Machine(axes, Array.Empty<AxisGroup>(), ioPoints, cylinders, workHeads, magazines, alarms);
    }
}
