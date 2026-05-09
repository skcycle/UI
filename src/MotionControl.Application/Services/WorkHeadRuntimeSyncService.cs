using System.Linq;
using MotionControl.Application.Interfaces;
using MotionControl.Domain.Entities;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class WorkHeadRuntimeSyncService(Machine machine)
    : RuntimeSyncServiceBase<WorkHeadConfigItem, WorkHead>(machine), IWorkHeadRuntimeSyncService
{
    public override Task ApplyAsync(WorkHeadConfigItem workHead, CancellationToken cancellationToken = default)
    {
        EnsureIoPointExists(workHead.VacuumOutputAddress, isOutput: true);
        EnsureIoPointExists(workHead.BlowOutputAddress, isOutput: true);
        EnsureIoPointExists(workHead.VacuumInputAddress, isOutput: false);
        EnsureIoPointExists(workHead.GeneralOutputAddress1, isOutput: true);
        EnsureIoPointExists(workHead.GeneralOutputAddress2, isOutput: true);
        EnsureIoPointExists(workHead.GeneralInputAddress1, isOutput: false);
        EnsureIoPointExists(workHead.GeneralInputAddress2, isOutput: false);

        var existing = FindExisting(workHead, w => w.Name);
        if (existing is null)
        {
            Machine.AddWorkHead(new WorkHead(
                workHead.Name, workHead.Description,
                workHead.XAxisNo, workHead.YAxisNo, workHead.ZAxisNo, workHead.RAxisNo,
                workHead.VacuumOutputAddress, workHead.BlowOutputAddress, workHead.VacuumInputAddress,
                workHead.GeneralOutputAddress1, workHead.GeneralOutputAddress2,
                workHead.GeneralInputAddress1, workHead.GeneralInputAddress2,
                workHead.VacuumTimeoutMs,
                workHead.Positions.Select(p => new WorkHeadPosition(p.Name, p.Description, p.X, p.Y, p.Z, p.R)).ToList(),
                workHead.SafeZ));
        }
        else
        {
            existing.UpdateMetadata(
                workHead.Name, workHead.Description,
                workHead.XAxisNo, workHead.YAxisNo, workHead.ZAxisNo, workHead.RAxisNo,
                workHead.VacuumOutputAddress, workHead.BlowOutputAddress, workHead.VacuumInputAddress,
                workHead.GeneralOutputAddress1, workHead.GeneralOutputAddress2,
                workHead.GeneralInputAddress1, workHead.GeneralInputAddress2,
                workHead.VacuumTimeoutMs, workHead.SafeZ);
            existing.Positions.Clear();
            foreach (var p in workHead.Positions)
                existing.AddPosition(new WorkHeadPosition(p.Name, p.Description, p.X, p.Y, p.Z, p.R));
        }
        return Task.CompletedTask;
    }

    protected override IEnumerable<WorkHead> GetAllRuntime() => Machine.WorkHeads;

    protected override string GetConfigName(WorkHeadConfigItem config) => config.Name;

    public override Task RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        Machine.RemoveWorkHead(name);
        return Task.CompletedTask;
    }

    protected override void RemoveRuntime(WorkHead item) => Machine.RemoveWorkHead(item.Name);
}
