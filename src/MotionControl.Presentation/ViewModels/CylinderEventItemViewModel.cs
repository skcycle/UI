using MotionControl.Control.Services;

namespace MotionControl.Presentation.ViewModels;

public sealed class CylinderEventItemViewModel
{
    public CylinderEventItemViewModel(CylinderEventRecord record)
    {
        Time = record.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        CylinderName = record.CylinderName;
        EventType = record.EventType;
        Description = record.Message;
    }

    public string Time { get; }
    public string CylinderName { get; }
    public string EventType { get; }
    public string Description { get; }
}
