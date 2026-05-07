using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Validation;

/// <summary>
/// Axis 配置校验器。
/// 负责应用层的基础参数合法性校验，避免规则只停留在 UI 层。
/// </summary>
public static class AxisConfigValidator
{
    public static void Validate(AxisMappingItem item)
    {
        if (item.AxisNo < 0)
        {
            throw new InvalidOperationException("AxisNo 不能小于 0");
        }

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            throw new InvalidOperationException("Axis 名称不能为空");
        }

        if (item.WorkVelocity.HasValue && item.WorkVelocity.Value <= 0)
        {
            throw new InvalidOperationException("WorkVelocity 必须大于 0");
        }

        if (item.SetupVelocity.HasValue && item.SetupVelocity.Value <= 0)
        {
            throw new InvalidOperationException("SetupVelocity 必须大于 0");
        }

        if (item.Acceleration.HasValue && item.Acceleration.Value <= 0)
        {
            throw new InvalidOperationException("Acceleration 必须大于 0");
        }

        if (item.Deceleration.HasValue && item.Deceleration.Value <= 0)
        {
            throw new InvalidOperationException("Deceleration 必须大于 0");
        }

        if (item.PulseEquivalent.HasValue && item.PulseEquivalent.Value <= 0)
        {
            throw new InvalidOperationException("PulseEquivalent 必须大于 0");
        }

        if (item.SoftLimitPositive.HasValue && item.SoftLimitNegative.HasValue && item.SoftLimitPositive.Value <= item.SoftLimitNegative.Value)
        {
            throw new InvalidOperationException("SoftLimitPositive 必须大于 SoftLimitNegative");
        }

        // 仅用于校验，不承担持久化查询职责
    }
}
