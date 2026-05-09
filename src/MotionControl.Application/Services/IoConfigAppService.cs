using System.Text.Json;
using System.Text.Json.Serialization;
using MotionControl.Application.Interfaces;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

/// <summary>
/// 使用 AppSettingsRepository 统一配置文件读写，支持原子写入（掉电安全）。
/// </summary>
public sealed class IoConfigAppService : IIoConfigAppService
{
    private readonly AppSettingsRepository _repo;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public IoConfigAppService(string appSettingsPath)
    {
        _repo = new AppSettingsRepository(appSettingsPath);
    }

    public async Task<IReadOnlyList<IoPointConfigItem>> LoadIoPointsAsync(CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        return root.IoMapping.Points
            .OrderBy(item => item.IsOutput)
            .ThenBy(item => item.Address)
            .ToList();
    }

    public async Task<IoPointConfigItem> AddIoPointAsync(bool isOutput, CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
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
        await _repo.SaveAsync(root, cancellationToken);
        return item;
    }

    public async Task<bool> DeleteIoPointAsync(bool isOutput, int address, CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        var removed = root.IoMapping.Points.RemoveAll(point => point.IsOutput == isOutput && point.Address == address) > 0;
        if (!removed) return false;
        await _repo.SaveAsync(root, cancellationToken);
        return true;
    }

    public async Task SaveIoPointsAsync(IEnumerable<IoPointConfigItem> ioPoints, CancellationToken cancellationToken = default)
    {
        var normalized = ioPoints.OrderBy(item => item.IsOutput).ThenBy(item => item.Address).ToList();
        var dup = normalized.Where(item => !item.IsOutput).GroupBy(item => item.Address).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) throw new InvalidOperationException($"DI address duplicated: {dup.Key}");
        dup = normalized.Where(item => item.IsOutput).GroupBy(item => item.Address).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) throw new InvalidOperationException($"DO address duplicated: {dup.Key}");

        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        root.IoMapping.Points = normalized;
        await _repo.SaveAsync(root, cancellationToken);
    }
}
