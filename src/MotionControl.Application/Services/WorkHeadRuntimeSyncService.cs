using MotionControl.Application.Interfaces;
using MotionControl.Domain.Entities;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class WorkHeadRuntimeSyncService(Machine machine) : IWorkHeadRuntimeSyncService
{
    public Task ApplyAsync(WorkHeadConfigItem workHead, CancellationToken cancellationToken = default)
    {
        EnsureIoPointExists(workHead.VacuumOutputAddress, true);
        EnsureIoPointExists(workHead.BlowOutputAddress, true);
        EnsureIoPointExists(workHead.VacuumInputAddress, false);
        EnsureIoPointExists(workHead.GeneralOutputAddress1, true);
        EnsureIoPointExists(workHead.GeneralOutputAddress2, true);
        EnsureIoPointExists(workHead.GeneralInputAddress1, false);
        EnsureIoPointExists(workHead.GeneralInputAddress2, false);

        var existing = machine.WorkHeads.FirstOrDefault(item => string.Equals(item.Name, workHead.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            machine.AddWorkHead(new WorkHead(workHead.Name, workHead.Description, workHead.XAxisNo, workHead.YAxisNo, workHead.ZAxisNo, workHead.RAxisNo, workHead.VacuumOutputAddress, workHead.BlowOutputAddress, workHead.VacuumInputAddress, workHead.GeneralOutputAddress1, workHead.GeneralOutputAddress2, workHead.GeneralInputAddress1, workHead.GeneralInputAddress2, workHead.VacuumTimeoutMs,
                workHead.Positions.Select(p => new WorkHeadPosition(p.Name, p.Description, p.X, p.Y, p.Z, p.R)).ToList(), workHead.SafeZ));
            return Task.CompletedTask;
        }

        existing.UpdateMetadata(workHead.Name, workHead.Description, workHead.XAxisNo, workHead.YAxisNo, workHead.ZAxisNo, workHead.RAxisNo, workHead.VacuumOutputAddress, workHead.BlowOutputAddress, workHead.VacuumInputAddress, workHead.GeneralOutputAddress1, workHead.GeneralOutputAddress2, workHead.GeneralInputAddress1, workHead.GeneralInputAddress2, workHead.VacuumTimeoutMs, workHead.SafeZ);
        existing.Positions.Clear();
        foreach (var p in workHead.Positions)
        {
            existing.AddPosition(new WorkHeadPosition(p.Name, p.Description, p.X, p.Y, p.Z, p.R));
        }
        return Task.CompletedTask;
    }

    public Task ReloadAsync(IEnumerable<WorkHeadConfigItem> workHeads, CancellationToken cancellationToken = default)
    {
        foreach (var item in machine.WorkHeads.ToList()) machine.RemoveWorkHead(item.Name);
        foreach (var item in workHeads) ApplyAsync(item, cancellationToken);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        machine.RemoveWorkHead(name);
        return Task.CompletedTask;
    }

    private void EnsureIoPointExists(int address, bool isOutput)
    {
        if (address < 0 || machine.IoPoints.Any(item => item.IsOutput == isOutput && item.Address == address)) return;
        machine.AddIoPoint(new IoPoint($"{(isOutput ? "DO" : "DI")} {address}", address, isOutput, "Auto-created for WorkHead"));
    }
}
