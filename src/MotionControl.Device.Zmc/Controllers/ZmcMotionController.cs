using MotionControl.Device.Abstractions.Controllers;
using MotionControl.Device.Abstractions.Models;
using MotionControl.Device.Abstractions.Results;
using MotionControl.Device.Zmc.Config;
using MotionControl.Device.Zmc.Native;
using MotionControl.Device.Zmc.Translators;

namespace MotionControl.Device.Zmc.Controllers;

public sealed class ZmcMotionController(
    ZmcControllerOptions options,
    ZmcStatusTranslator statusTranslator,
    ZmcAxisNativeFacade axisNativeFacade,
    IEtherCatStatusProvider etherCatStatusProvider) : IAxisMotionController, IIoController, IEtherCatController, ISafetyController
{
    public async Task<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            var result = axisNativeFacade.Connect(options.IpAddress);
            return result == 0 ? DeviceResult.Ok() : DeviceResult.Fail($"控制器连接失败 (native={result})");
        }, cancellationToken);
    }

    public async Task<DeviceResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            var result = axisNativeFacade.Disconnect();
            return result == 0 ? DeviceResult.Ok() : DeviceResult.Fail($"控制器断开连接失败 (native={result})");
        }, cancellationToken);
    }

    public async Task<AxisFeedback> GetAxisFeedbackAsync(int axisNo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            float dpos = 0;
            float mpos = 0;
            float speed = 0;
            var idle = 1;
            var axisStatus = 0;
            var homeStatus = 0;
            var busEnableStatus = 0;

            var dposOk = axisNativeFacade.GetAxisDpos(axisNo, ref dpos) == 0;
            var mposOk = axisNativeFacade.GetAxisMpos(axisNo, ref mpos) == 0;
            var speedOk = axisNativeFacade.GetAxisSpeed(axisNo, ref speed) == 0;

            var status2Result = axisNativeFacade.GetAxisStatus2(axisNo, -1, ref axisStatus, ref idle, ref homeStatus, ref busEnableStatus);
            if (status2Result != 0)
            {
                // Fallback 链：两步都检查返回值，任一步失败 → 整个反馈无效
                var idleOk = axisNativeFacade.GetAxisIdle(axisNo, ref idle) == 0;
                var statusOk = axisNativeFacade.GetAxisStatus(axisNo, ref axisStatus) == 0;
                if (!idleOk || !statusOk)
                    return AxisFeedback.Invalid(axisNo);
            }

            var axisEnable = 0;
            var enableOk = axisNativeFacade.GetAxisEnable(axisNo, ref axisEnable) == 0;

            if (!dposOk || !mposOk)
                return AxisFeedback.Invalid(axisNo);

            return statusTranslator.Translate(axisNo, dpos, mpos, speed, idle, axisStatus, homeStatus, enableOk ? axisEnable : busEnableStatus);
        }, cancellationToken);
    }

    public async Task<float> GetAxisMseepAsync(int axisNo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            float value = 0;
            var r = axisNativeFacade.GetAxisMseep(axisNo, ref value);
            return r == 0 ? value : float.NaN;
        }, cancellationToken);
    }

    public async Task<float> GetAxisMspeedAsync(int axisNo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            float value = 0;
            var r = axisNativeFacade.GetAxisMspeed(axisNo, ref value);
            return r == 0 ? value : float.NaN;
        }, cancellationToken);
    }

    public async Task<AxisCaptureSnapshot> GetAxisCaptureSnapshotAsync(int axisNo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() => GetAxisCaptureSnapshot(axisNo), cancellationToken);
    }

    public AxisCaptureSnapshot GetAxisCaptureSnapshot(int axisNo)
    {
        float dpos = 0;
        float mpos = 0;
        float mspeed = 0;

        var dposOk = axisNativeFacade.GetAxisDpos(axisNo, ref dpos) == 0;
        var mposOk = axisNativeFacade.GetAxisMpos(axisNo, ref mpos) == 0;
        var mspeedOk = axisNativeFacade.GetAxisMspeed(axisNo, ref mspeed) == 0;

        return dposOk && mposOk && mspeedOk
            ? new AxisCaptureSnapshot(true, dpos, mpos, mspeed)
            : AxisCaptureSnapshot.Invalid;
    }

    public async Task<DeviceResult> EnableAxisAsync(int axisNo, CancellationToken cancellationToken = default)
        => await RunAsync(() => axisNativeFacade.EnableAxis(axisNo),
            ok: DeviceResult.Ok(),
            fail: r => DeviceResult.Fail($"轴 {axisNo} 使能失败 (native={r})"),
            cancellationToken);

    public async Task<DeviceResult> DisableAxisAsync(int axisNo, CancellationToken cancellationToken = default)
        => await RunAsync(() => axisNativeFacade.DisableAxis(axisNo),
            ok: DeviceResult.Ok(),
            fail: r => DeviceResult.Fail($"轴 {axisNo} 失能失败 (native={r})"),
            cancellationToken);

    public async Task<DeviceResult> HomeAxisAsync(int axisNo, CancellationToken cancellationToken = default)
        => await RunAsync(() => axisNativeFacade.HomeAxis(axisNo),
            ok: DeviceResult.Ok(),
            fail: r => DeviceResult.Fail($"轴 {axisNo} 回零失败 (native={r})"),
            cancellationToken);

    public async Task<DeviceResult> MoveAbsoluteAsync(int axisNo, AxisMoveCommand command, CancellationToken cancellationToken = default)
        => await RunAsync(
            () => axisNativeFacade.MoveAbsolute(axisNo, command.Position, command.Velocity, command.Acceleration, command.Deceleration),
            ok: DeviceResult.Ok(),
            fail: r => DeviceResult.Fail($"轴 {axisNo} 定位失败 (native={r})"),
            cancellationToken);

    public async Task<DeviceResult> JogAxisAsync(int axisNo, double velocity, bool positiveDirection, CancellationToken cancellationToken = default)
        => await RunAsync(() => axisNativeFacade.JogAxis(axisNo, velocity, positiveDirection),
            ok: DeviceResult.Ok(),
            fail: r => DeviceResult.Fail($"轴 {axisNo} Jog 失败 (native={r})"),
            cancellationToken);

    public async Task<DeviceResult> StopAxisAsync(int axisNo, CancellationToken cancellationToken = default)
        => await RunAsync(() => axisNativeFacade.StopAxis(axisNo),
            ok: DeviceResult.Ok(),
            fail: r => DeviceResult.Fail($"轴 {axisNo} 停止失败 (native={r})"),
            cancellationToken);

    public async Task<DeviceResult> RapidStopAsync(int mode, CancellationToken cancellationToken = default)
        => await RunAsync(() => axisNativeFacade.RapidStop(mode),
            ok: DeviceResult.Ok(),
            fail: r => DeviceResult.Fail($"快速停止失败 (native={r})"),
            cancellationToken);

    public async Task<DeviceResult> ResetAxisAlarmAsync(int axisNo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            var driveResult = axisNativeFacade.ClearDriveAlarm(axisNo);
            if (driveResult != 0)
                return DeviceResult.Fail($"轴 {axisNo} 驱动器报警清除失败 (native={driveResult})");

            var zmcResult = axisNativeFacade.ClearZmcAlarm(axisNo);
            if (zmcResult != 0)
                return DeviceResult.Fail($"轴 {axisNo} 自身报警清除失败 (native={zmcResult})");

            return DeviceResult.Ok();
        }, cancellationToken);
    }

    public async Task<bool> GetIoPointValueAsync(int address, bool isOutput, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            uint value = 0;
            int result;
            if (isOutput)
                result = axisNativeFacade.GetOutput(address, ref value);
            else
                result = axisNativeFacade.GetInput(address, ref value);

            if (result != 0)
                throw new InvalidOperationException(
                    $"IO read failed: {(isOutput ? "output" : "input")} addr={address} native={result}");

            return value != 0;
        }, cancellationToken);
    }

    public async Task<DeviceResult> SetIoPointValueAsync(int address, bool value, CancellationToken cancellationToken = default)
        => await RunAsync(() => axisNativeFacade.SetOutput(address, value ? 1 : 0),
            ok: DeviceResult.Ok(),
            fail: r => DeviceResult.Fail($"DO {address} 输出失败 (native={r})"),
            cancellationToken);

    public async Task<IoPointValue[]> GetIoPointValuesBatchAsync((int address, bool isOutput)[] points, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            var results = new IoPointValue[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var (addr, isOutput) = points[i];
                if (cancellationToken.IsCancellationRequested) break;
                uint value = 0;
                var result = isOutput
                    ? axisNativeFacade.GetOutput(addr, ref value)
                    : axisNativeFacade.GetInput(addr, ref value);
                results[i] = new IoPointValue(addr, isOutput, result == 0 && value != 0);
            }
            return results;
        }, cancellationToken);
    }

    public async Task<AxisFeedback[]> GetAxisFeedbacksBatchAsync(int[] axisNos, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            var results = new AxisFeedback[axisNos.Length];
            for (int i = 0; i < axisNos.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var axisNo = axisNos[i];

                float dpos = 0, mpos = 0, speed = 0;
                var idle = 1;
                var axisStatus = 0;
                var homeStatus = 0;
                var busEnableStatus = 0;

                var dposOk = axisNativeFacade.GetAxisDpos(axisNo, ref dpos) == 0;
                var mposOk = axisNativeFacade.GetAxisMpos(axisNo, ref mpos) == 0;
                axisNativeFacade.GetAxisSpeed(axisNo, ref speed);

                var status2Result = axisNativeFacade.GetAxisStatus2(axisNo, -1, ref axisStatus, ref idle, ref homeStatus, ref busEnableStatus);
                if (status2Result != 0)
                {
                    axisNativeFacade.GetAxisIdle(axisNo, ref idle);
                    axisNativeFacade.GetAxisStatus(axisNo, ref axisStatus);
                }

                var axisEnable = 0;
                var enableOk = axisNativeFacade.GetAxisEnable(axisNo, ref axisEnable) == 0;

                if (!dposOk || !mposOk)
                {
                    results[i] = AxisFeedback.Invalid(axisNo);
                    continue;
                }

                results[i] = statusTranslator.Translate(axisNo, dpos, mpos, speed, idle, axisStatus, homeStatus, enableOk ? axisEnable : busEnableStatus);
            }
            return results;
        }, cancellationToken);
    }

    public async Task<EtherCatControllerStatus> GetControllerStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            if (axisNativeFacade.IsConnected)
            {
                var probeResult = axisNativeFacade.ProbeConnection();
                if (probeResult != 0)
                {
                    axisNativeFacade.Disconnect();
                }
            }

            return etherCatStatusProvider.CreateStatus(axisNativeFacade.IsConnected, options.AxisCount);
        }, cancellationToken);
    }

    private static async Task<DeviceResult> RunAsync(
        Func<int> command,
        DeviceResult ok,
        Func<int, DeviceResult> fail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            var result = command();
            return result == 0 ? ok : fail(result);
        }, cancellationToken);
    }
}
