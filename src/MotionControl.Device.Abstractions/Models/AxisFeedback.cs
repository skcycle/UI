using MotionControl.Domain.Enums;

namespace MotionControl.Device.Abstractions.Models;

public sealed class AxisFeedback
{
    public int AxisNo { get; init; }
    public double CommandPosition { get; init; }
    public double EncoderPosition { get; init; }
    public double CurrentVelocity { get; init; }
    public AxisState AxisState { get; init; }
    public ServoState ServoState { get; init; }
    public bool HasAlarm { get; init; }
    public bool IsHomed { get; init; }
    public bool PositiveHardLimitTriggered { get; init; }
    public bool NegativeHardLimitTriggered { get; init; }
    public bool PositiveSoftLimitTriggered { get; init; }
    public bool NegativeSoftLimitTriggered { get; init; }

    /// <summary>
    /// 反馈数据是否有效。false 表示底层读取失败，数据为默认值/占位值，
    /// 调用方应跳过本轮状态更新。
    /// </summary>
    public bool IsValid { get; init; } = true;

    /// <summary>创建一个表示读取失败的无效反馈。轴号保留用于日志追踪。</summary>
    public static AxisFeedback Invalid(int axisNo) => new()
    {
        AxisNo = axisNo,
        IsValid = false,
        AxisState = AxisState.Disabled,
        ServoState = ServoState.Off,
    };
}
