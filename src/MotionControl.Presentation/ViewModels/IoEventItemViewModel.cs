using MotionControl.Control.Services;

namespace MotionControl.Presentation.ViewModels;

public sealed class IoEventItemViewModel
{
    public IoEventItemViewModel(IoEventRecord record)
    {
        Time = record.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        Direction = record.IsOutput ? "DO" : "DI";
        Address = record.Address.ToString();
        State = record.Value ? "ON" : "OFF";
        Description = record.Message;
    }

    public string Time { get; }
    public string Direction { get; }
    public string Address { get; }
    public string State { get; }
    public string Description { get; }
}
