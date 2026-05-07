using System.Runtime.InteropServices;
using System.Text;

namespace MotionControl.Device.Zmc.Native;

public static class ZmcNativeApi
{
    [DllImport("zauxdll.dll", EntryPoint = "ZAux_OpenEth", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int OpenEth(string ipaddr, out IntPtr handle);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Close", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int Close(IntPtr handle);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Execute", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int Execute(IntPtr handle, string command, StringBuilder response, uint responseLength);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_SetAtype", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectSetAtype(IntPtr handle, int axisNo, int value);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_Single_MoveAbs", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectSingleMoveAbs(IntPtr handle, int axisNo, float distance);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_Single_Vmove", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectSingleVmove(IntPtr handle, int axisNo, int direction);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_Single_Cancel", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectSingleCancel(IntPtr handle, int axisNo, int mode);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_GetDpos", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectGetDpos(IntPtr handle, int axisNo, ref float value);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_GetMpos", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectGetMpos(IntPtr handle, int axisNo, ref float value);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_GetSpeed", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectGetSpeed(IntPtr handle, int axisNo, ref float value);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_GetMspeed", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectGetMspeed(IntPtr handle, int axisNo, ref float value);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_GetIfIdle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectGetIfIdle(IntPtr handle, int axisNo, ref int value);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_GetAxisStatus", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectGetAxisStatus(IntPtr handle, int axisNo, ref int value);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_GetAxisStatus2", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectGetAxisStatus2(IntPtr handle, int axisNo, int homeIn, ref int axisStatus, ref int idle, ref int homeStatus, ref int busEnableStatus);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_GetIn", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectGetIn(IntPtr handle, int ioNo, ref uint value);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_GetOp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectGetOp(IntPtr handle, int ioNo, ref uint value);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_SetOp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectSetOp(IntPtr handle, int ioNo, int value);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_SetAxisEnable", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectSetAxisEnable(IntPtr handle, int axisNo, int value);

    [DllImport("zauxdll.dll", EntryPoint = "ZAux_Direct_GetAxisEnable", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int DirectGetAxisEnable(IntPtr handle, int axisNo, ref int value);

    // ── EtherCAT Bus API ──

    /// <summary>获取总线扫描到的节点数量</summary>
    [DllImport("zauxdll.dll", EntryPoint = "ZAux_BusCmd_GetNodeNum", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int BusCmdGetNodeNum(IntPtr handle, int slot, ref int value);

    /// <summary>读取总线节点信息（sel: 0-厂商ID 1-设备ID 2-版本 10-IN数 11-OUT数）</summary>
    [DllImport("zauxdll.dll", EntryPoint = "ZAux_BusCmd_GetNodeInfo", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int BusCmdGetNodeInfo(IntPtr handle, uint slot, uint node, uint sel, ref int value);

    /// <summary>读取总线节点通讯状态</summary>
    [DllImport("zauxdll.dll", EntryPoint = "ZAux_BusCmd_GetNodeStatus", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    public static extern int BusCmdGetNodeStatus(IntPtr handle, uint slot, uint node, ref uint status);
}
