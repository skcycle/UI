using System.Text.Json;
using System.Text.Json.Serialization;

namespace MotionControl.Infrastructure.Configuration;

/// <summary>
/// 统一配置文件读写仓储。
/// 所有业务 ConfigService 的配置文件读写统一走这里，避免重复代码和掉电风险。
///
/// <para><b>原子写入：</b>先写临时文件，再 rename，掉电最多丢一次写。</para>
/// <para><b>保留备份：</b>每次写入前将旧文件Rename为 .bak。</para>
/// <para><b>统一序列化：</b>所有读写共用同一套 JsonSerializerOptions。</para>
/// </summary>
public sealed class AppSettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;

    public AppSettingsRepository(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// 加载整个 appsettings.json 文件，反序列化为指定根类型。
    /// 文件不存在时返回 default(T)。
    /// </summary>
    public async Task<T> LoadAsync<T>(CancellationToken cancellationToken = default) where T : new()
    {
        if (!File.Exists(_filePath))
            return new T();

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
    }

    /// <summary>
    /// 原子写入整个 appsettings.json 文件。
    /// 先写 .tmp，再 Rename 覆盖原文件，最后删除 .bak。
    /// 掉电最多丢一次写，不会在中途留下损坏文件。
    /// </summary>
    public async Task SaveAsync<T>(T root, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(_filePath) ?? ".";
        Directory.CreateDirectory(dir);

        var tmpPath = _filePath + ".tmp";
        var bakPath = _filePath + ".bak";

        var json = JsonSerializer.Serialize(root, JsonOptions);

        // 1. 写临时文件
        await File.WriteAllTextAsync(tmpPath, json, cancellationToken);

        // 2. 如果原文件存在，先备份
        if (File.Exists(_filePath))
        {
            if (File.Exists(bakPath))
                File.Delete(bakPath);
            File.Move(_filePath, bakPath);
        }

        // 3. 用临时文件替换原文件
        File.Move(tmpPath, _filePath, overwrite: true);

        // 4. 删除备份（写入成功后清理）
        if (File.Exists(bakPath))
            File.Delete(bakPath);
    }

    /// <summary>
    /// 只加载指定 section（T 必须对应 appsettings.json 中的子节点属性）。
    /// 实现方式：加载整个文件，再提取目标 section。
    /// </summary>
    public async Task<TRoot> LoadSectionAsync<TRoot>(CancellationToken cancellationToken = default) where TRoot : new()
    {
        return await LoadAsync<TRoot>(cancellationToken);
    }

    /// <summary>
    /// 保存时只更新指定 section。
    /// 读取当前文件 → 合并更新目标 section → 原子写回。
    /// </summary>
    public async Task SaveSectionAsync<TRoot, TSection>(
        Func<TRoot, TSection> sectionSelector,
        Action<TRoot, TSection> sectionSetter,
        CancellationToken cancellationToken = default) where TRoot : new()
    {
        var root = await LoadAsync<TRoot>(cancellationToken);
        var current = sectionSelector(root);
        sectionSetter(root, current);
        await SaveAsync(root, cancellationToken);
    }
}
