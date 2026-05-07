using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MotionControl.Application.Interfaces;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class PositionSetupConfigAppService(string appSettingsPath) : IPositionSetupConfigAppService
{
    public async Task<IReadOnlyList<PositionSetupConfigItem>> LoadPositionsAsync(CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        return root.PositionSetupMapping.Positions
            .Select(Normalize)
            .OrderBy(item => item.Name)
            .ToList();
    }

    public async Task<PositionSetupConfigItem> AddPositionAsync(CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        var index = root.PositionSetupMapping.Positions.Count + 1;
        var item = new PositionSetupConfigItem
        {
            Name = $"PositionSetup {index}",
            Positions = new List<PositionSetupPositionConfigItem>
            {
                new() { Name = "Position 1" }
            }
        };
        root.PositionSetupMapping.Positions.Add(item);
        await SaveRootAsync(root, cancellationToken);
        return item;
    }

    public async Task SavePositionsAsync(IEnumerable<PositionSetupConfigItem> positions, CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        root.PositionSetupMapping.Positions = positions
            .Select(Normalize)
            .OrderBy(item => item.Name)
            .ToList();
        await SaveRootAsync(root, cancellationToken);
    }

    private async Task<AppSettingsRoot> LoadRootAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(appSettingsPath)) return new AppSettingsRoot();
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

    private static PositionSetupConfigItem Normalize(PositionSetupConfigItem item)
    {
        item.Positions ??= new List<PositionSetupPositionConfigItem>();
        return item;
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
