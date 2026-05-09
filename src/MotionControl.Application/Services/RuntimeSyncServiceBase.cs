using System.Linq;
using MotionControl.Domain.Entities;

namespace MotionControl.Application.Services;

/// <summary>
/// RuntimeSync 系列服务模板，消除 Apply/Reload/Remove 的重复模式。
/// TConfig: 配置项类型（如 CylinderConfigItem, WorkHeadConfigItem）
/// TRuntime: 运行时实体类型（如 Cylinder, WorkHead）
/// </summary>
public abstract class RuntimeSyncServiceBase<TConfig, TRuntime> where TRuntime : class
{
    protected readonly Machine Machine;

    protected RuntimeSyncServiceBase(Machine machine)
    {
        Machine = machine;
    }

    /// <summary>Apply 一个配置项到运行时（查找或创建）。子类实现具体创建逻辑。</summary>
    public abstract Task ApplyAsync(TConfig config, CancellationToken cancellationToken = default);

    /// <summary>Reload：清空运行时集合，再批量 Apply。</summary>
    public async Task ReloadAsync(IEnumerable<TConfig> configs, CancellationToken cancellationToken = default)
    {
        var toRemove = GetAllRuntime().ToList();
        foreach (var item in toRemove)
        {
            RemoveRuntime(item);
        }

        foreach (var config in configs)
        {
            await ApplyAsync(config, cancellationToken);
        }
    }

    /// <summary>确保指定地址的 IO 点已存在于 Machine，不存在则自动创建。</summary>
    protected void EnsureIoPointExists(int address, bool isOutput)
    {
        if (address < 0) return;
        if (Machine.IoPoints.Any(p => p.IsOutput == isOutput && p.Address == address)) return;
        Machine.AddIoPoint(new IoPoint(
            (isOutput ? "DO" : "DI") + $" {address}",
            address,
            isOutput,
            $"Auto-created for {typeof(TConfig).Name}"));
    }

    /// <summary>查找运行时是否已有对应配置项（按名称匹配）。</summary>
    protected TRuntime? FindExisting(TConfig config, Func<TRuntime, string> nameSelector)
    {
        return GetAllRuntime().FirstOrDefault(r =>
            string.Equals(nameSelector(r), GetConfigName(config), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>获取所有运行时实体，由子类实现。</summary>
    protected abstract System.Collections.Generic.IEnumerable<TRuntime> GetAllRuntime();

    /// <summary>从运行时删除指定实体，由子类实现。</summary>
    public abstract Task RemoveAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>从运行时删除实体，由子类实现。</summary>
    protected abstract void RemoveRuntime(TRuntime item);

    /// <summary>获取配置项的名称字段。</summary>
    protected abstract string GetConfigName(TConfig config);
}
