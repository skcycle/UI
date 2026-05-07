using System.Linq;
using MotionControl.Device.Abstractions.Models;
using MotionControl.Domain.Enums;

namespace MotionControl.Device.Zmc.Translators;

public sealed class ZmcStatusTranslator
{
    private const int FollowingErrorAlarmBit = 1 << 1;
    private const int PlcCommErrorBit = 1 << 2;
    private const int DriveAlarmBit = 1 << 3;
    private const int PositiveHardLimitBit = 1 << 4;
    private const int NegativeHardLimitBit = 1 << 5;
    private const int FollowingErrorOutOfRangeBit = 1 << 8;
    private const int PositiveSoftLimitBit = 1 << 9;
    private const int NegativeSoftLimitBit = 1 << 10;
    private const int AxisWarningInputBit = 1 << 22;
    private const int AxisInAlarmStateBit = 1 << 23;

    public AxisFeedback Translate(int axisNo, float dpos, float mpos, float speed, int idle, int axisStatus, int homeStatus, int busEnableStatus)
    {
        var axisState = idle == 0 ? AxisState.Moving : AxisState.Standstill;
        var servoState = busEnableStatus != 0 ? ServoState.On : ServoState.Off;
        var hasAlarm = HasAny(axisStatus,
            FollowingErrorAlarmBit, PlcCommErrorBit, DriveAlarmBit,
            FollowingErrorOutOfRangeBit, AxisWarningInputBit, AxisInAlarmStateBit);
        var isHomed = homeStatus != 0;
        var positiveHardLimitTriggered = HasAny(axisStatus, PositiveHardLimitBit);
        var negativeHardLimitTriggered = HasAny(axisStatus, NegativeHardLimitBit);
        var positiveSoftLimitTriggered = HasAny(axisStatus, PositiveSoftLimitBit);
        var negativeSoftLimitTriggered = HasAny(axisStatus, NegativeSoftLimitBit);

        return new AxisFeedback
        {
            AxisNo = axisNo,
            CommandPosition = dpos,
            EncoderPosition = mpos,
            CurrentVelocity = speed,
            AxisState = axisState,
            ServoState = servoState,
            HasAlarm = hasAlarm,
            IsHomed = isHomed,
            PositiveHardLimitTriggered = positiveHardLimitTriggered,
            NegativeHardLimitTriggered = negativeHardLimitTriggered,
            PositiveSoftLimitTriggered = positiveSoftLimitTriggered,
            NegativeSoftLimitTriggered = negativeSoftLimitTriggered,
            IsValid = true,
        };
    }

    private static bool HasAny(int axisStatus, params int[] masks)
        => masks.Any(mask => (axisStatus & mask) == mask);
}
