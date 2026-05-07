namespace MotionControl.Device.Abstractions.Results;

public sealed record DeviceResult(bool Success, string? ErrorMessage = null)
{
    public static DeviceResult Ok() => new(true);
    public static DeviceResult Fail(string errorMessage) => new(false, errorMessage);
}

public sealed record DeviceResult<T>(bool Success, T? Value = default, string? ErrorMessage = null)
{
    public static DeviceResult<T> Ok(T value) => new(true, value);
    public static DeviceResult<T> Fail(string errorMessage) => new(false, default, errorMessage);
}
