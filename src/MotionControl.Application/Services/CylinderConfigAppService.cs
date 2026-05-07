using System.Text.Json;
using System.Text.Json.Serialization;
using MotionControl.Application.Interfaces;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class CylinderConfigAppService(string appSettingsPath) : ICylinderConfigAppService
{
    public async Task<IReadOnlyList<CylinderConfigItem>> LoadCylindersAsync(CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        return root.CylinderMapping.Cylinders.OrderBy(item => item.Name).ToList();
    }

    public async Task<CylinderConfigItem> AddCylinderAsync(CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        var index = root.CylinderMapping.Cylinders.Count;
        var item = new CylinderConfigItem
        {
            Name = $"Cylinder_{index}",
            Description = string.Empty,
            ExtendSensorInputAddress = 0,
            RetractSensorInputAddress = -1,
            ExtendOutputAddress = 0,
            RetractOutputAddress = -1,
            ActionTimeoutMs = 3000
        };

        root.CylinderMapping.Cylinders.Add(item);
        await SaveRootAsync(root, cancellationToken);
        return item;
    }

    public async Task<bool> DeleteCylinderAsync(string name, CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        var removed = root.CylinderMapping.Cylinders.RemoveAll(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed)
        {
            return false;
        }

        await SaveRootAsync(root, cancellationToken);
        return true;
    }

    public async Task SaveCylindersAsync(IEnumerable<CylinderConfigItem> cylinders, CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        root.CylinderMapping.Cylinders = cylinders.OrderBy(item => item.Name).ToList();
        await SaveRootAsync(root, cancellationToken);
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

    private async Task SaveRootAsync(AppSettingsRoot root, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(root, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
        await File.WriteAllTextAsync(appSettingsPath, json, cancellationToken);
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
