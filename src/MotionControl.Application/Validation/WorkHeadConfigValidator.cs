using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Validation;

public static class WorkHeadConfigValidator
{
    public static void Validate(IReadOnlyCollection<WorkHeadConfigItem> items)
    {
        if (items.Any(item => string.IsNullOrWhiteSpace(item.Name)))
        {
            throw new InvalidOperationException("WorkHead 名称不能为空");
        }

        if (items.Any(item => item.VacuumOutputAddress < -1 || item.BlowOutputAddress < -1 || item.VacuumInputAddress < -1 || item.GeneralOutputAddress1 < -1 || item.GeneralOutputAddress2 < -1 || item.GeneralInputAddress1 < -1 || item.GeneralInputAddress2 < -1))
        {
            throw new InvalidOperationException("WorkHead DI/DO 地址不能小于 -1");
        }

        if (items.Any(item => item.VacuumTimeoutMs <= 0))
        {
            throw new InvalidOperationException("WorkHead VacuumTimeoutMs 必须大于 0");
        }

        var duplicateName = items.GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicateName is not null)
        {
            throw new InvalidOperationException($"WorkHead 名称重复: {duplicateName.Key}");
        }

        foreach (var item in items)
        {
            if (item.VacuumOutputAddress >= 0 && item.VacuumOutputAddress == item.BlowOutputAddress)
            {
                throw new InvalidOperationException($"WorkHead {item.Name} 的 Vacuum DO 和 Blow DO 不能相同");
            }
        }

        ValidateUniqueOutput(items);
        ValidateUniqueInput(items);
    }

    private static void ValidateUniqueOutput(IEnumerable<WorkHeadConfigItem> items)
    {
        var duplicate = items
            .SelectMany(item => new[]
            {
                (item.Name, Address: item.VacuumOutputAddress, Label: "Vacuum DO"),
                (item.Name, Address: item.BlowOutputAddress, Label: "Blow DO"),
                (item.Name, Address: item.GeneralOutputAddress1, Label: "General DO 1"),
                (item.Name, Address: item.GeneralOutputAddress2, Label: "General DO 2")
            })
            .Where(x => x.Address >= 0)
            .GroupBy(x => x.Address)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            var details = string.Join(", ", duplicate.Select(x => $"{x.Name}:{x.Label}"));
            throw new InvalidOperationException($"WorkHead DO 地址重复: {duplicate.Key}，涉及 {details}");
        }
    }

    private static void ValidateUniqueInput(IEnumerable<WorkHeadConfigItem> items)
    {
        var duplicate = items
            .SelectMany(item => new[]
            {
                (item.Name, Address: item.VacuumInputAddress, Label: "Vacuum DI"),
                (item.Name, Address: item.GeneralInputAddress1, Label: "General DI 1"),
                (item.Name, Address: item.GeneralInputAddress2, Label: "General DI 2")
            })
            .Where(x => x.Address >= 0)
            .GroupBy(x => x.Address)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            var details = string.Join(", ", duplicate.Select(x => $"{x.Name}:{x.Label}"));
            throw new InvalidOperationException($"WorkHead DI 地址重复: {duplicate.Key}，涉及 {details}");
        }
    }
}
