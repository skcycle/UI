using System.Linq;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Validation;

/// <summary>
/// IO 配置校验器。
/// 负责应用层的地址唯一性和基础字段合法性校验。
/// </summary>
public static class IoConfigValidator
{
    public static void Validate(IReadOnlyCollection<IoPointConfigItem> items)
    {
        var duplicateAddress = items
            .GroupBy(io => new { io.IsOutput, io.Address })
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateAddress is not null)
        {
            throw new InvalidOperationException($"存在重复地址: {(duplicateAddress.Key.IsOutput ? "DO" : "DI")} {duplicateAddress.Key.Address}");
        }

        var invalidItem = items.FirstOrDefault(io => string.IsNullOrWhiteSpace(io.Name) || io.Address < 0);
        if (invalidItem is not null)
        {
            throw new InvalidOperationException("IO 配置存在空名称或非法地址");
        }
    }
}
