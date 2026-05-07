using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MotionControl.Device.Zmc.Native;

/// <summary>
/// ZMC 控制器原生 API 的外观封装，提供线程安全的连接管理和轴操作。
///
/// <para><b>线程安全模型</b></para>
/// <list type="bullet">
/// <item><b>ReaderWriterLockSlim</b> 保护 _handle 生命周期：
///   所有操作（读/写轴参数、IO、探活）获取读锁（可并发）；
///   Connect/Disconnect 获取写锁（排他，等待所有读锁释放后执行）。</item>
/// <item><b>_connectLock</b> 串行化 Connect 调用，防止多线程同时建联。</item>
/// <item><b>_multiStepLock</b> 串行化 MoveAbsolute/JogAxis/WriteAxisParameters
///   等复合操作，防止参数设置与执行指令被交错。</item>
/// <item>Disconnect 在写锁内执行：先置 _handle=IntPtr.Zero 阻断新操作，
///   等待所有进行中的读锁释放，然后安全 Close。不存在 handle 竞态。</item>
/// </list>
///
/// <para><b>多轴并发</b></para>
/// 单步操作（读状态、IO、使能/失能）可真正并发 — SDK 已证明
/// 单实例多线程用；复合操作（MoveAbsolute 等）因 _multiStepLock 会串行化
/// 所有轴的运动指令序列。这是保守安全策略，与控制器单命令队列的物理现实一致。
///
/// <para><b>⚠ 锁顺序规则（新增方法必须遵守）</b></para>
/// 1. 单步操作：只拿 _handleLock 读锁（无需 _multiStepLock）
/// 2. 复合操作：先拿 _handleLock 读锁，再拿 _multiStepLock，释放时反序
/// 3. Connect/Disconnect：拿 _handleLock 写锁（排他，等所有读写锁释放）
/// <b>禁止</b>：先拿 _multiStepLock 再拿 _handleLock（会导致与 Connect 死锁）
/// </summary>
public sealed class ZmcAxisNativeFacade
{
    private const string ConnectionProbeCommand = "?SPEED(0)";

    private readonly object _connectLock = new();
    private readonly ReaderWriterLockSlim _handleLock = new();
    private readonly object _multiStepLock = new();
    private readonly ILogger<ZmcAxisNativeFacade> _logger;
    private IntPtr _handle = IntPtr.Zero;

    public ZmcAxisNativeFacade() : this(NullLogger<ZmcAxisNativeFacade>.Instance) { }

    public ZmcAxisNativeFacade(ILogger<ZmcAxisNativeFacade> logger)
    {
        _logger = logger ?? NullLogger<ZmcAxisNativeFacade>.Instance;
    }

    // ── 连接状态 ──

    public bool IsConnected
    {
        get
        {
            _handleLock.EnterReadLock();
            try { return _handle != IntPtr.Zero; }
            finally { _handleLock.ExitReadLock(); }
        }
    }

    // ── 连接管理（写锁） ──

    public int Connect(string ipAddress)
    {
        lock (_connectLock)
        {
            _handleLock.EnterWriteLock();
            try
            {
                if (_handle != IntPtr.Zero)
                {
                    _logger.LogWarning("ZMC reconnecting: closing existing handle before new connect to {IpAddress}", ipAddress);
                    SafeClose(_handle);
                    _handle = IntPtr.Zero;
                }

                _logger.LogDebug("ZMC OpenEth connecting to {IpAddress}", ipAddress);
                var result = ZmcNativeApi.OpenEth(ipAddress, out var newHandle);
                if (result != 0)
                {
                    _logger.LogError("ZMC OpenEth failed: error={Result} ip={IpAddress}", result, ipAddress);
                    _handle = IntPtr.Zero;
                    return result;
                }

                _handle = newHandle;
                _logger.LogInformation("ZMC connected to {IpAddress}", ipAddress);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ZMC OpenEth exception for {IpAddress}", ipAddress);
                _handle = IntPtr.Zero;
                return -1;
            }
            finally
            {
                _handleLock.ExitWriteLock();
            }
        }
    }

    public int Disconnect()
    {
        _handleLock.EnterWriteLock();
        try
        {
            if (_handle == IntPtr.Zero) return 0;
            _logger.LogDebug("ZMC disconnecting");
            var handle = _handle;
            _handle = IntPtr.Zero;
            return SafeClose(handle);
        }
        finally
        {
            _handleLock.ExitWriteLock();
        }
    }

    // ── 单步操作（读锁，可并发） ──

    public int EnableAxis(int axisNo)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("EnableAxis");
            try
            {
                var r = ZmcNativeApi.DirectSetAxisEnable(_handle, axisNo, 1);
                if (r != 0) _logger.LogWarning("ZMC EnableAxis failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC EnableAxis exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int DisableAxis(int axisNo)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("DisableAxis");
            try
            {
                var r = ZmcNativeApi.DirectSetAxisEnable(_handle, axisNo, 0);
                if (r != 0) _logger.LogWarning("ZMC DisableAxis failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC DisableAxis exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int GetAxisEnable(int axisNo, ref int value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("GetAxisEnable");
            try
            {
                var r = ZmcNativeApi.DirectGetAxisEnable(_handle, axisNo, ref value);
                if (r != 0) _logger.LogWarning("ZMC GetAxisEnable failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC GetAxisEnable exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int HomeAxis(int axisNo)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("HomeAxis");
            try
            {
                var r = ExecuteNativeCommand(_handle, $"DATUM({axisNo})");
                if (r != 0) _logger.LogWarning("ZMC HomeAxis failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC HomeAxis exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int ClearDriveAlarm(int axisNo)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("ClearDriveAlarm");
            try
            {
                var r = ExecuteNativeCommand(_handle, $"ALMCLR({axisNo})");
                if (r != 0) _logger.LogWarning("ZMC ClearDriveAlarm failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC ClearDriveAlarm exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int ClearZmcAlarm(int axisNo)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("ClearZmcAlarm");
            try
            {
                var r = ExecuteNativeCommand(_handle, $"DATUM({axisNo},0)");
                if (r != 0) _logger.LogWarning("ZMC ClearZmcAlarm failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC ClearZmcAlarm exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int StopAxis(int axisNo)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("StopAxis");
            try
            {
                var r = ZmcNativeApi.DirectSingleCancel(_handle, axisNo, 2);
                if (r != 0) _logger.LogWarning("ZMC StopAxis failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC StopAxis exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int RapidStop(int mode)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("RapidStop");
            try
            {
                var r = ExecuteNativeCommand(_handle, $"RAPIDSTOP({mode})");
                if (r != 0) _logger.LogWarning("ZMC RapidStop failed: result={Result} mode={Mode}", r, mode);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC RapidStop exception: mode={Mode}", mode); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int GetAxisDpos(int axisNo, ref float value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("GetDpos");
            try
            {
                var r = ZmcNativeApi.DirectGetDpos(_handle, axisNo, ref value);
                if (r != 0) _logger.LogWarning("ZMC GetDpos failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC GetDpos exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int GetAxisMpos(int axisNo, ref float value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("GetMpos");
            try
            {
                var r = ZmcNativeApi.DirectGetMpos(_handle, axisNo, ref value);
                if (r != 0) _logger.LogWarning("ZMC GetMpos failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC GetMpos exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int GetAxisSpeed(int axisNo, ref float value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("GetSpeed");
            try
            {
                var r = ZmcNativeApi.DirectGetSpeed(_handle, axisNo, ref value);
                if (r != 0) _logger.LogWarning("ZMC GetSpeed failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC GetSpeed exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    /// <summary>
    /// 读取 MSEEP 寄存器（编码器位置，脉冲数）。
    /// </summary>
    public int GetAxisMseep(int axisNo, ref float value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("GetMseep");
            try
            {
                var buffer = new StringBuilder(64);
                var r = ZmcNativeApi.Execute(_handle, $"?MSEEP({axisNo})", buffer, 64);
                if (r == 0 && float.TryParse(buffer.ToString(), out var parsed))
                {
                    value = parsed;
                    return 0;
                }
                _logger.LogWarning("ZMC GetMseep failed: result={Result} axis={AxisNo} buffer={Buffer}", r, axisNo, buffer);
                return r != 0 ? r : -1;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC GetMseep exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    /// <summary>
    /// 读取 MSPEED（电机当前速度，直接接口）。
    /// </summary>
    public int GetAxisMspeed(int axisNo, ref float value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("GetMspeed");
            try
            {
                var r = ZmcNativeApi.DirectGetMspeed(_handle, axisNo, ref value);
                if (r != 0) _logger.LogWarning("ZMC GetMspeed failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC GetMspeed exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int GetAxisIdle(int axisNo, ref int value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("GetIdle");
            try
            {
                var r = ZmcNativeApi.DirectGetIfIdle(_handle, axisNo, ref value);
                if (r != 0) _logger.LogWarning("ZMC GetIdle failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC GetIdle exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int GetAxisStatus(int axisNo, ref int value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("GetAxisStatus");
            try
            {
                var r = ZmcNativeApi.DirectGetAxisStatus(_handle, axisNo, ref value);
                if (r != 0) _logger.LogWarning("ZMC GetAxisStatus failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC GetAxisStatus exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int GetAxisStatus2(int axisNo, int homeIn, ref int axisStatus, ref int idle, ref int homeStatus, ref int busEnableStatus)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("GetAxisStatus2");
            try
            {
                var r = ZmcNativeApi.DirectGetAxisStatus2(_handle, axisNo, homeIn, ref axisStatus, ref idle, ref homeStatus, ref busEnableStatus);
                if (r != 0) _logger.LogWarning("ZMC GetAxisStatus2 failed: result={Result} axis={AxisNo}", r, axisNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC GetAxisStatus2 exception: axis={AxisNo}", axisNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int GetInput(int ioNo, ref uint value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("GetInput");
            try
            {
                var r = ZmcNativeApi.DirectGetIn(_handle, ioNo, ref value);
                if (r != 0) _logger.LogWarning("ZMC GetInput failed: result={Result} io={IoNo}", r, ioNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC GetInput exception: io={IoNo}", ioNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int GetOutput(int ioNo, ref uint value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("GetOutput");
            try
            {
                var r = ZmcNativeApi.DirectGetOp(_handle, ioNo, ref value);
                if (r != 0) _logger.LogWarning("ZMC GetOutput failed: result={Result} io={IoNo}", r, ioNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC GetOutput exception: io={IoNo}", ioNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int SetOutput(int ioNo, int value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("SetOutput");
            try
            {
                var r = ZmcNativeApi.DirectSetOp(_handle, ioNo, value);
                if (r != 0) _logger.LogWarning("ZMC SetOutput failed: result={Result} io={IoNo}", r, ioNo);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC SetOutput exception: io={IoNo}", ioNo); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int ProbeConnection()
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return -1;
            try
            {
                var buffer = new StringBuilder(64);
                var r = ZmcNativeApi.Execute(_handle, ConnectionProbeCommand, buffer, 64);
                if (r != 0) _logger.LogWarning("ZMC ProbeConnection failed: result={Result}", r);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC ProbeConnection exception"); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public string ReadAxisParameters(int axisNo)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return "Controller not connected";
            try
            {
                var buffer = new StringBuilder(512);
                var result = ZmcNativeApi.Execute(_handle, $"?SPEED({axisNo})", buffer, 512);
                if (result != 0)
                {
                    _logger.LogWarning("ReadAxisParameters(?SPEED) failed: axis={AxisNo} result={Result}", axisNo, result);
                    return $"Read SPEED failed: {result}";
                }
                return buffer.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReadAxisParameters(?SPEED) exception: axis={AxisNo}", axisNo);
                return $"Read SPEED exception: {ex.Message}";
            }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    // ── EtherCAT 总线操作（读锁） ──

    public int GetBusNodeCount(ref int count)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("BusNodeCount");
            try
            {
                var r = ZmcNativeApi.BusCmdGetNodeNum(_handle, 0, ref count);
                if (r != 0) _logger.LogWarning("ZMC BusNodeCount failed: result={Result}", r);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC BusNodeCount exception"); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int GetBusNodeInfo(uint node, uint sel, ref int value)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("BusNodeInfo");
            try
            {
                var r = ZmcNativeApi.BusCmdGetNodeInfo(_handle, 0, node, sel, ref value);
                if (r != 0) _logger.LogWarning("ZMC BusNodeInfo failed: result={Result} node={Node} sel={Sel}", r, node, sel);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC BusNodeInfo exception: node={Node} sel={Sel}", node, sel); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int GetBusNodeStatus(uint node, ref uint status)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("BusNodeStatus");
            try
            {
                var r = ZmcNativeApi.BusCmdGetNodeStatus(_handle, 0, node, ref status);
                if (r != 0) _logger.LogWarning("ZMC BusNodeStatus failed: result={Result} node={Node}", r, node);
                return r;
            }
            catch (Exception ex) { _logger.LogError(ex, "ZMC BusNodeStatus exception: node={Node}", node); return -1; }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    // ── 多步复合操作（读锁 + _multiStepLock） ──

    public int MoveAbsolute(int axisNo, double position, double velocity, double acceleration, double deceleration)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("MoveAbsolute");

            lock (_multiStepLock)
            {
                try
                {
                    var r1 = ExecuteNativeCommand(_handle, $"SPEED({axisNo})={velocity}");
                    if (r1 != 0) { _logger.LogWarning("ZMC MoveAbsolute/SPEED failed: result={Result} axis={AxisNo}", r1, axisNo); return r1; }
                    var r2 = ExecuteNativeCommand(_handle, $"ACCEL({axisNo})={acceleration}");
                    if (r2 != 0) { _logger.LogWarning("ZMC MoveAbsolute/ACCEL failed: result={Result} axis={AxisNo}", r2, axisNo); return r2; }
                    var r3 = ExecuteNativeCommand(_handle, $"DECEL({axisNo})={deceleration}");
                    if (r3 != 0) { _logger.LogWarning("ZMC MoveAbsolute/DECEL failed: result={Result} axis={AxisNo}", r3, axisNo); return r3; }
                    var r4 = ZmcNativeApi.DirectSingleMoveAbs(_handle, axisNo, (float)position);
                    if (r4 != 0) { _logger.LogWarning("ZMC MoveAbsolute/MoveAbs failed: result={Result} axis={AxisNo}", r4, axisNo); return r4; }
                    return 0;
                }
                catch (Exception ex) { _logger.LogError(ex, "ZMC MoveAbsolute exception: axis={AxisNo}", axisNo); return -1; }
            }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int JogAxis(int axisNo, double velocity, bool positiveDirection)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("JogAxis");

            lock (_multiStepLock)
            {
                try
                {
                    var setup = ExecuteNativeCommand(_handle, $"SPEED({axisNo})={velocity}");
                    if (setup != 0) { _logger.LogWarning("ZMC JogAxis/SPEED failed: result={Result} axis={AxisNo}", setup, axisNo); return setup; }
                    var r = ZmcNativeApi.DirectSingleVmove(_handle, axisNo, positiveDirection ? 1 : -1);
                    if (r != 0) { _logger.LogWarning("ZMC JogAxis/Vmove failed: result={Result} axis={AxisNo}", r, axisNo); return r; }
                    return 0;
                }
                catch (Exception ex) { _logger.LogError(ex, "ZMC JogAxis exception: axis={AxisNo}", axisNo); return -1; }
            }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    public int WriteAxisParameters(int axisNo, double workVelocity, double setupVelocity)
    {
        _handleLock.EnterReadLock();
        try
        {
            if (_handle == IntPtr.Zero) return LogNotConnected("WriteAxisParams");

            lock (_multiStepLock)
            {
                try
                {
                    var r1 = ExecuteNativeCommand(_handle, $"SPEED({axisNo})={workVelocity}");
                    if (r1 != 0) { _logger.LogWarning("ZMC WriteAxisParams/SPEED failed: result={Result} axis={AxisNo}", r1, axisNo); return r1; }
                    var r2 = ExecuteNativeCommand(_handle, $"CREEP({axisNo})={setupVelocity}");
                    if (r2 != 0) { _logger.LogWarning("ZMC WriteAxisParams/CREEP failed: result={Result} axis={AxisNo}", r2, axisNo); return r2; }
                    return 0;
                }
                catch (Exception ex) { _logger.LogError(ex, "ZMC WriteAxisParams exception: axis={AxisNo}", axisNo); return -1; }
            }
        }
        finally { _handleLock.ExitReadLock(); }
    }

    // ── 内部方法 ──

    private static int ExecuteNativeCommand(IntPtr handle, string command)
    {
        var buffer = new StringBuilder(256);
        return ZmcNativeApi.Execute(handle, command, buffer, 256);
    }

    private int LogNotConnected(string operation)
    {
        _logger.LogWarning("ZMC {Operation} skipped: not connected", operation);
        return -1;
    }

    private int SafeClose(IntPtr handle)
    {
        try { return ZmcNativeApi.Close(handle); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZMC Close exception");
            return -1;
        }
    }
}
