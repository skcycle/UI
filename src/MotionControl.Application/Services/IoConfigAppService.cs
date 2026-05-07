using System.Text.Json;
using System.Text.Json.Serialization;
using MotionControl.Application.Interfaces;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class IoConfigAppService(string appSettingsPath) : IIoConfigAppService
{
    public async Task<IReadOnlyList<IoPointConfigItem>> LoadIoPointsAsync(CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        return root.IoMapping.Points
            .OrderBy(item => item.IsOutput)
            .ThenBy(item => item.Address)
            .ToList();
    }

    public async Task<IoPointConfigItem> AddIoPointAsync(bool isOutput, CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        var points = root.IoMapping.Points.Where(point => point.IsOutput == isOutput).ToList();
        var nextAddress = points.Count == 0 ? 0 : points.Max(point => point.Address) + 1;
        var prefix = isOutput ? "DO" : "DI";
        var item = new IoPointConfigItem
        {
            Name = $"{prefix}_{nextAddress}",
            Address = nextAddress,
            IsOutput = isOutput,
            Description = string.Empty
        };

        root.IoMapping.Points.Add(item);
        await SaveRootAsync(root, cancellationToken);
        return item;
    }

    public async Task<bool> DeleteIoPointAsync(bool isOutput, int address, CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        var removed = root.IoMapping.Points.RemoveAll(point => point.IsOutput == isOutput && point.Address == address) > 0;
        if (!removed)
        {
            return false;
        }

        await SaveRootAsync(root, cancellationToken);
        return true;
    }

    public async Task SaveIoPointsAsync(IEnumerable<IoPointConfigItem> ioPoints, CancellationToken cancellationToken = default)
    {
        var normalized = ioPoints.OrderBy(item => item.IsOutput).ThenBy(item => item.Address).ToList();
        var duplicatedInput = normalized.Where(item => !item.IsOutput)
            .GroupBy(item => item.Address)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatedInput is not null)
        {
            throw new InvalidOperationException($"DI address duplicated: {duplicatedInput.Key}");
        }

        var duplicatedOutput = normalized.Where(item => item.IsOutput)
            .GroupBy(item => item.Address)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatedOutput is not null)
        {
            throw new InvalidOperationException($"DO address duplicated: {duplicatedOutput.Key}");
        }

        var root = await LoadRootAsync(cancellationToken);
        root.IoMapping.Points = normalized;
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
