using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Validation;

public static class CylinderConfigValidator
{
    public static void Validate(IReadOnlyCollection<CylinderConfigItem> items)
    {
        if (items.Any(item => string.IsNullOrWhiteSpace(item.Name)))
        {
            throw new InvalidOperationException("Cylinder 名称不能为空");
        }

        if (items.Any(item => item.ExtendSensorInputAddress < -1 || item.RetractSensorInputAddress < -1 || item.ExtendOutputAddress < -1 || item.RetractOutputAddress < -1))
        {
            throw new InvalidOperationException("Cylinder 地址不能小于 -1");
        }

        if (items.Any(item => item.ActionTimeoutMs <= 0))
        {
            throw new InvalidOperationException("Cylinder 动作超时时间必须大于 0 ms");
        }

        var duplicateName = items.GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicateName is not null)
        {
            throw new InvalidOperationException($"Cylinder 名称重复: {duplicateName.Key}");
        }

        foreach (var item in items)
        {
            if (item.ExtendSensorInputAddress >= 0 && item.RetractSensorInputAddress >= 0 && item.ExtendSensorInputAddress == item.RetractSensorInputAddress)
            {
                throw new InvalidOperationException($"Cylinder {item.Name} 的 Extend DI 和 Retract DI 不能相同");
            }

            if (item.ExtendOutputAddress >= 0 && item.RetractOutputAddress >= 0 && item.ExtendOutputAddress == item.RetractOutputAddress)
            {
                throw new InvalidOperationException($"Cylinder {item.Name} 的 Extend DO 和 Retract DO 不能相同");
            }
        }

        ValidateDuplicateAddresses(items, item => item.ExtendSensorInputAddress, "Extend DI");
        ValidateDuplicateAddresses(items, item => item.RetractSensorInputAddress, "Retract DI");
        ValidateDuplicateAddresses(items, item => item.ExtendOutputAddress, "Extend DO");
        ValidateDuplicateAddresses(items, item => item.RetractOutputAddress, "Retract DO");
        ValidateCrossOutputConflicts(items);
        ValidateCrossInputConflicts(items);
    }

    private static void ValidateDuplicateAddresses(IEnumerable<CylinderConfigItem> items, Func<CylinderConfigItem, int> selector, string label)
    {
        var duplicate = items
            .Where(item => selector(item) >= 0)
            .GroupBy(selector)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            var names = string.Join(", ", duplicate.Select(item => item.Name));
            throw new InvalidOperationException($"Cylinder {label} 地址重复: {duplicate.Key}，涉及 {names}");
        }
    }

    private static void ValidateCrossOutputConflicts(IEnumerable<CylinderConfigItem> items)
    {
        var conflicts = items
            .SelectMany(item => new[]
            {
                (item.Name, Address: item.ExtendOutputAddress, Role: "Extend DO"),
                (item.Name, Address: item.RetractOutputAddress, Role: "Retract DO")
            })
            .Where(item => item.Address >= 0)
            .GroupBy(item => item.Address)
            .FirstOrDefault(group => group.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);

        if (conflicts is not null)
        {
            var refs = string.Join(", ", conflicts.Select(item => $"{item.Name}:{item.Role}"));
            throw new InvalidOperationException($"Cylinder DO 地址冲突: {conflicts.Key}，涉及 {refs}");
        }
    }

    private static void ValidateCrossInputConflicts(IEnumerable<CylinderConfigItem> items)
    {
        var conflicts = items
            .SelectMany(item => new[]
            {
                (item.Name, Address: item.ExtendSensorInputAddress, Role: "Extend DI"),
                (item.Name, Address: item.RetractSensorInputAddress, Role: "Retract DI")
            })
            .Where(item => item.Address >= 0)
            .GroupBy(item => item.Address)
            .FirstOrDefault(group => group.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);

        if (conflicts is not null)
        {
            var refs = string.Join(", ", conflicts.Select(item => $"{item.Name}:{item.Role}"));
            throw new InvalidOperationException($"Cylinder DI 地址冲突: {conflicts.Key}，涉及 {refs}");
        }
    }
}
