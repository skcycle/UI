namespace MotionControl.Device.Abstractions.Models;

/// <summary>
/// 轴采集快照，包含命令位置、编码器位置、电机速度。
/// 用于波形采集，在一次线程池任务中完成所有读数，避免多次异步调用的并发问题。
/// </summary>
public readonly record struct AxisCaptureSnapshot(
    bool IsValid,
    double CommandPosition,
    double EncoderPosition,
    double MotorSpeed)
{
    public static AxisCaptureSnapshot Invalid => new(false, 0, 0, 0);
}
