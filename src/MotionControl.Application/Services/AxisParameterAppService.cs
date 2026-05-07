using System.Text.Json;
using System.Text.Json.Serialization;
using MotionControl.Application.Interfaces;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class AxisParameterAppService(string appSettingsPath) : IAxisParameterAppService
{
    public async Task<List<AxisMappingItem>> LoadAllAxesAsync(CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        return root.AxisMapping.Axes.ToList();
    }

    public async Task SaveAllAxesAsync(List<AxisMappingItem> items, CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        root.AxisMapping.Axes = items;
        await SaveRootAsync(root, cancellationToken);
    }

    public async Task<AxisMappingItem?> LoadAxisParametersAsync(int axisNo, CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        return root.AxisMapping.Axes.FirstOrDefault(axis => axis.AxisNo == axisNo);
    }

    public async Task SaveAxisParametersAsync(AxisMappingItem axisMappingItem, CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        SaveAxisParameters(root, axisMappingItem);
        await SaveRootAsync(root, cancellationToken);
    }

    public async Task<AxisMappingItem> AddAxisAsync(CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
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
        await SaveRootAsync(root, cancellationToken);
        return axis;
    }

    public async Task<bool> DeleteAxisAsync(int axisNo, CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        var removed = root.AxisMapping.Axes.RemoveAll(axis => axis.AxisNo == axisNo) > 0;
        if (!removed)
        {
            return false;
        }

        if (root.AxisMapping.AxisNames.Count > axisNo)
        {
            root.AxisMapping.AxisNames[axisNo] = $"Axis {axisNo}";
        }

        await SaveRootAsync(root, cancellationToken);
        return true;
    }

    private static void SaveAxisParameters(AppSettingsRoot root, AxisMappingItem axisMappingItem)
    {
        var existing = root.AxisMapping.Axes.FirstOrDefault(axis => axis.AxisNo == axisMappingItem.AxisNo);
        if (existing is null)
        {
            root.AxisMapping.Axes.Add(axisMappingItem);
        }
        else
        {
            existing.Name = axisMappingItem.Name;
            existing.Group = axisMappingItem.Group;
            existing.IsMaster = axisMappingItem.IsMaster;
            existing.MasterAxisName = axisMappingItem.MasterAxisName;
            existing.SoftLimitPositive = axisMappingItem.SoftLimitPositive;
            existing.SoftLimitNegative = axisMappingItem.SoftLimitNegative;
            existing.WorkVelocity = axisMappingItem.WorkVelocity;
            existing.SetupVelocity = axisMappingItem.SetupVelocity;
            existing.PulseEquivalent = axisMappingItem.PulseEquivalent;
            existing.HomeMode = axisMappingItem.HomeMode;
            existing.ServoBinding = axisMappingItem.ServoBinding;
        }

        if (root.AxisMapping.AxisNames.Count <= axisMappingItem.AxisNo)
        {
            while (root.AxisMapping.AxisNames.Count <= axisMappingItem.AxisNo)
            {
                root.AxisMapping.AxisNames.Add($"Axis {root.AxisMapping.AxisNames.Count}");
            }
        }

        root.AxisMapping.AxisNames[axisMappingItem.AxisNo] = axisMappingItem.Name;
    }

    private async Task SaveRootAsync(AppSettingsRoot root, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(appSettingsPath, json, cancellationToken);
    }

    private async Task<AppSettingsRoot> LoadRootAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(appSettingsPath))
        {
            throw new FileNotFoundException($"App settings file not found: {appSettingsPath}");
        }

        var json = await File.ReadAllTextAsync(appSettingsPath, cancellationToken);
        return JsonSerializer.Deserialize<AppSettingsRoot>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        }) ?? new AppSettingsRoot();
    }

    private sealed class AppSettingsRoot
    {
        public ZmcControllerConfig ZmcController { get; set; } = new();
        public AxisMappingOptions AxisMapping { get; set; } = new();
        public IoMappingOptions IoMapping { get; set; } = new();
        public CylinderMappingOptions CylinderMapping { get; set; } = new();
        public MagazineMappingOptions MagazineMapping { get; set; } = new();
        public WorkHeadMappingOptions WorkHeadMapping { get; set; } = new();
        public PositionSetupMappingOptions PositionSetupMapping { get; set; } = new();
    }

    private sealed class ZmcControllerConfig
    {
        public string IpAddress { get; set; } = "127.0.0.1";
        public int AxisCount { get; set; } = 32;
        public int PollingIntervalMs { get; set; } = 200;
    }
}
