namespace MotionControl.Device.Abstractions.Models;

/// <summary>
/// 单个 IO 点的快照值。
/// </summary>
public readonly record struct IoPointValue(int Address, bool IsOutput, bool Value);
