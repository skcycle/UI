using System.Text.Json;

namespace MotionControl.Control.Services;

/// <summary>
/// 统一构造各模块 PayloadJson 的 Helper。
/// 所有事件记录的结构化数据都通过这里写入，确保字段一致。
/// </summary>
public static class PayloadBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Magazine ─────────────────────────────────────────────

    public static string Magazine(int slotNo, int layerIndex, int layerCount, double layerHeight,
        int settlingMs, string? scanResult = null, string? status = null)
    {
        return Serialize(new
        {
            slotNo,
            layerIndex,
            layerCount,
            layerHeight,
            settlingMs,
            scanResult,
            status
        });
    }

    public static string MagazinePick(int slotNo, int layerIndex, int layerCount, double layerHeight, int settlingMs)
        => Magazine(slotNo, layerIndex, layerCount, layerHeight, settlingMs, status: "pick");

    public static string MagazinePlace(int slotNo, int layerIndex, int layerCount, double layerHeight, int settlingMs, string? scanResult = null)
        => Magazine(slotNo, layerIndex, layerCount, layerHeight, settlingMs, scanResult, status: "place");

    public static string MagazineScan(int slotNo, int layerIndex, int layerCount, double layerHeight, string scanResult)
        => Magazine(slotNo, layerIndex, layerCount, layerHeight, settlingMs: 0, scanResult, status: "scan");

    // ── PositionSetup ────────────────────────────────────────

    public static string PositionSetup(string setupItem, string positionName, string stage,
        int? failedAxis = null, string? errorMessage = null)
    {
        return Serialize(new
        {
            setupItem,
            positionName,
            stage,
            failedAxis,
            errorMessage
        });
    }

    public static string PositionSetupMove(string setupItem, string positionName, string stage)
        => PositionSetup(setupItem, positionName, stage, failedAxis: null, errorMessage: null);

    public static string PositionSetupFail(string setupItem, string positionName, string stage, int failedAxis, string errorMessage)
        => PositionSetup(setupItem, positionName, stage, failedAxis, errorMessage);

    // ── WorkHead ─────────────────────────────────────────────

    public static string WorkHead(string action, string objectName, double? targetX = null,
        double? targetY = null, double? targetZ = null, double? targetR = null,
        double? velocity = null, string? result = null, int? durationMs = null)
    {
        return Serialize(new
        {
            action,
            objectName,
            target = new { x = targetX, y = targetY, z = targetZ, r = targetR },
            velocity,
            result,
            durationMs
        });
    }

    public static string WorkHeadMove(string objectName, double x, double y, double z, double r, double velocity)
        => WorkHead("move", objectName, x, y, z, r, velocity);

    public static string WorkHeadAction(string action, string objectName, string? result = null, int? durationMs = null)
        => WorkHead(action, objectName, result: result, durationMs: durationMs);

    // ── IO ───────────────────────────────────────────────────

    public static string IO(int address, bool isOutput, bool expectedValue, bool actualValue,
        string? source = null, int? durationMs = null)
    {
        return Serialize(new
        {
            address,
            isOutput,
            source,
            expectedValue,
            actualValue,
            durationMs,
            match = actualValue == expectedValue
        });
    }

    public static string IOCheck(int address, bool expected, bool actual, string? source = null)
        => IO(address, isOutput: false, expected, actual, source);

    public static string IOSet(int address, bool value, string? source = null)
        => IO(address, isOutput: true, value, value, source);

    // ── AxisCommand ──────────────────────────────────────────

    public static string AxisCommand(string commandName, int axisNo, string? targetPosition = null,
        double? velocity = null, string? result = null)
    {
        return Serialize(new
        {
            commandName,
            axisNo,
            targetPosition,
            velocity,
            result
        });
    }

    // ── Helper ───────────────────────────────────────────────

    public static string Serialize<T>(T obj) where T : class
    {
        try
        {
            return JsonSerializer.Serialize(obj, JsonOptions);
        }
        catch
        {
            return "{}";
        }
    }
}
