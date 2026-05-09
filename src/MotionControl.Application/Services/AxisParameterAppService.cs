using System.Text.Json;
using System.Text.Json.Serialization;
using MotionControl.Application.Interfaces;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

/// <summary>
/// 使用 AppSettingsRepository 统一配置文件读写，支持原子写入（掉电安全）。
/// </summary>
public sealed class AxisParameterAppService : IAxisParameterAppService
{
    private readonly AppSettingsRepository _repo;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AxisParameterAppService(string appSettingsPath)
    {
        _repo = new AppSettingsRepository(appSettingsPath);
    }

    public async Task<List<AxisMappingItem>> LoadAllAxesAsync(CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        return root.AxisMapping.Axes.ToList();
    }

    public async Task SaveAllAxesAsync(List<AxisMappingItem> items, CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        root.AxisMapping.Axes = items;
        await _repo.SaveAsync(root, cancellationToken);
    }

    public async Task<AxisMappingItem?> LoadAxisParametersAsync(int axisNo, CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        return root.AxisMapping.Axes.FirstOrDefault(axis => axis.AxisNo == axisNo);
    }

    public async Task SaveAxisParametersAsync(AxisMappingItem item, CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        SaveAxisParameters(root, item);
        await _repo.SaveAsync(root, cancellationToken);
    }

    public async Task<AxisMappingItem> AddAxisAsync(CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        var nextAxisNo = root.AxisMapping.Axes.Count == 0 ? 0 : root.AxisMapping.Axes.Max(axis => axis.AxisNo) + 1;
        var axis = new AxisMappingItem
        {
            AxisNo = nextAxisNo,
            Name = $"Axis {nextAxisNo}",
            Group = string.Empty,
            IsMaster = false,
            SoftLimitPositive = 1000,
            SoftLimitNegative = -1000,
            WorkVelocity = 200,
            SetupVelocity = 50,
            PulseEquivalent = 1000,
            HomeMode = MotionControl.Domain.Enums.HomeMode.Default,
            ServoBinding = string.Empty
        };
        SaveAxisParameters(root, axis);
        await _repo.SaveAsync(root, cancellationToken);
        return axis;
    }

    public async Task<bool> DeleteAxisAsync(int axisNo, CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        var removed = root.AxisMapping.Axes.RemoveAll(axis => axis.AxisNo == axisNo) > 0;
        if (!removed) return false;
        if (root.AxisMapping.AxisNames.Count > axisNo)
            root.AxisMapping.AxisNames[axisNo] = $"Axis {axisNo}";
        await _repo.SaveAsync(root, cancellationToken);
        return true;
    }

    private static void SaveAxisParameters(AppSettingsRoot root, AxisMappingItem item)
    {
        var existing = root.AxisMapping.Axes.FirstOrDefault(a => a.AxisNo == item.AxisNo);
        if (existing is null)
        {
            root.AxisMapping.Axes.Add(item);
        }
        else
        {
            existing.Name = item.Name;
            existing.Group = item.Group;
            existing.IsMaster = item.IsMaster;
            existing.MasterAxisName = item.MasterAxisName;
            existing.SoftLimitPositive = item.SoftLimitPositive;
            existing.SoftLimitNegative = item.SoftLimitNegative;
            existing.WorkVelocity = item.WorkVelocity;
            existing.SetupVelocity = item.SetupVelocity;
            existing.PulseEquivalent = item.PulseEquivalent;
            existing.HomeMode = item.HomeMode;
            existing.ServoBinding = item.ServoBinding;
        }

        while (root.AxisMapping.AxisNames.Count <= item.AxisNo)
            root.AxisMapping.AxisNames.Add($"Axis {root.AxisMapping.AxisNames.Count}");
        root.AxisMapping.AxisNames[item.AxisNo] = item.Name;
    }
}
