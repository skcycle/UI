using System.Linq;
using MotionControl.Device.Abstractions.Controllers;
using MotionControl.Device.Abstractions.Models;
using MotionControl.Domain.Entities;

namespace MotionControl.Control.Services;

public sealed class IoPollingService(
    IIoController motionController,
    Machine machine,
    IoEventRuntimeState ioEventRuntimeState)
{
    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        // 一次性批量读取所有 IO（单个 Task.Run，同步循环内读完）
        var points = machine.IoPoints
            .Select(io => (io.Address, io.IsOutput))
            .ToArray();
        if (points.Length == 0) return;

        IoPointValue[] results;
        try
        {
            results = await motionController.GetIoPointValuesBatchAsync(points, cancellationToken);
        }
        catch
        {
            return; // 整批读取失败，跳过本轮
        }

        var index = 0;
        foreach (var ioPoint in machine.IoPoints)
        {
            if (index >= results.Length) break;
            var snapshot = results[index++];

            var previousValue = ioPoint.Value;
            if (previousValue != snapshot.Value)
            {
                ioPoint.Update(snapshot.Value);
                ioEventRuntimeState.Add(new IoEventRecord
                {
                    Name = ioPoint.Name,
                    Address = ioPoint.Address,
                    IsOutput = ioPoint.IsOutput,
                    Value = snapshot.Value,
                    Message = $"{ioPoint.Name} -> {(snapshot.Value ? "ON" : "OFF")}",
                });
            }
        }
    }
}
