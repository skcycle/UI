using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Control.Services;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// DataGrid 行用 ViewModel，对 RuntimeEventLogEntry 做展示层包装。
/// </summary>
public sealed class RuntimeEventLogItemViewModel : INotifyPropertyChanged
{
    private readonly RuntimeEventLogEntry _entry;

    public RuntimeEventLogItemViewModel(RuntimeEventLogEntry entry)
    {
        _entry = entry;
    }

    public DateTime TimestampUtc => _entry.TimestampUtc;
    public string TimestampLocal => _entry.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
    public string Module => _entry.Module;
    public string EventType => _entry.EventType;
    public string Level => _entry.Level;
    public string? AxisNo => _entry.AxisNo?.ToString();
    public string? ObjectName => _entry.ObjectName;
    public string? Address => _entry.Address?.ToString();
    public string? IO => _entry.IsOutput.HasValue ? (_entry.IsOutput.Value ? "DO" : "DI") : null;
    public string? BoolValue => _entry.BoolValue?.ToString();
    public string? Status => _entry.Status;
    public string? CommandName => _entry.CommandName;
    public string? Message => _entry.Message;
    public string? PayloadJson => _entry.PayloadJson;

    // 列可见性（根据模块动态隐藏无关列）
    public bool ShowAxisNo => !string.IsNullOrEmpty(AxisNo);
    public bool ShowIO => IO != null;
    public bool ShowAddress => Address != null;
    public bool ShowCommandName => !string.IsNullOrEmpty(CommandName);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
