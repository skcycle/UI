using System.Text.Json;
using System.Text.Json.Serialization;
using MotionControl.Application.Interfaces;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class WorkHeadConfigAppService(string appSettingsPath) : IWorkHeadConfigAppService
{
    public async Task<IReadOnlyList<WorkHeadConfigItem>> LoadWorkHeadsAsync(CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        return root.WorkHeadMapping.WorkHeads.OrderBy(item => item.Name).ToList();
    }

    public async Task<WorkHeadConfigItem> AddWorkHeadAsync(CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        var index = root.WorkHeadMapping.WorkHeads.Count;
        var item = new WorkHeadConfigItem
        {
            Name = $"WorkHead {index + 1}",
            Description = string.Empty,
            XAxisNo = -1,
            YAxisNo = -1,
            ZAxisNo = -1,
            RAxisNo = -1,
            VacuumOutputAddress = -1,
            BlowOutputAddress = -1,
            VacuumInputAddress = -1,
            GeneralOutputAddress1 = -1,
            GeneralOutputAddress2 = -1,
            GeneralInputAddress1 = -1,
            GeneralInputAddress2 = -1
        };
        root.WorkHeadMapping.WorkHeads.Add(item);
        await SaveRootAsync(root, cancellationToken);
        return item;
    }

    public async Task<bool> DeleteWorkHeadAsync(string name, CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        var removed = root.WorkHeadMapping.WorkHeads.RemoveAll(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed) return false;
        await SaveRootAsync(root, cancellationToken);
        return true;
    }

    public async Task SaveWorkHeadsAsync(IEnumerable<WorkHeadConfigItem> workHeads, CancellationToken cancellationToken = default)
    {
        var root = await LoadRootAsync(cancellationToken);
        root.WorkHeadMapping.WorkHeads = workHeads.OrderBy(item => item.Name).ToList();
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
