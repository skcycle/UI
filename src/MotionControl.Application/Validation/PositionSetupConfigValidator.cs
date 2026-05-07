using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Validation;

public static class PositionSetupConfigValidator
{
    public static void Validate(IReadOnlyCollection<PositionSetupConfigItem> items)
    {
        if (items.Any(item => string.IsNullOrWhiteSpace(item.Name)))
        {
            throw new InvalidOperationException("位置设定名称不能为空");
        }

        var duplicateName = items.GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicateName is not null)
        {
            throw new InvalidOperationException($"位置设定名称重复: {duplicateName.Key}");
        }

        foreach (var item in items)
        {
            // ---- 父对象层校验 ----

            if (item.SafeZ < 0)
            {
                throw new InvalidOperationException($"位置设定 {item.Name} SafeZ 不能为负数");
            }

            var configuredAxes = new[] { item.XxAxisNo, item.XAxisNo, item.YAxisNo, item.ZAxisNo, item.UAxisNo, item.VAxisNo, item.WAxisNo }
                .Where(axis => axis >= 0)
                .ToList();

            var duplicateAxis = configuredAxes
                .GroupBy(axis => axis)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicateAxis is not null)
            {
                throw new InvalidOperationException($"位置设定 {item.Name} 存在重复轴号: {duplicateAxis.Key}");
            }

            // ---- 子位置点校验 ----

            if (item.Positions.Count == 0)
            {
                throw new InvalidOperationException($"位置设定 {item.Name} 至少需要配置一个位置点");
            }

            foreach (var pos in item.Positions)
            {
                if (string.IsNullOrWhiteSpace(pos.Name))
                {
                    throw new InvalidOperationException($"位置设定 {item.Name} 下存在未命名的位置点");
                }

                if (item.ZAxisNo < 0 && item.SafeZ != 0)
                {
                    throw new InvalidOperationException($"位置设定 {item.Name} 未配置 Z 轴时，SafeZ 必须为 0");
                }

                if (item.ZAxisNo >= 0 && item.SafeZ < pos.ZPosition)
                {
                    throw new InvalidOperationException($"位置设定 {item.Name} 的 SafeZ 不能低于位置点 {pos.Name} 的目标 Z");
                }
            }

            // 位置点名称在同对象内不能重复
            var duplicatePosName = item.Positions
                .GroupBy(pos => pos.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicatePosName is not null)
            {
                throw new InvalidOperationException($"位置设定 {item.Name} 下位置点名称重复: {duplicatePosName.Key}");
            }
        }
    }
}
