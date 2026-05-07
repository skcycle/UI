namespace MotionControl.Presentation.ViewModels;

public sealed class MagazineEventItemViewModel
{
    public MagazineEventItemViewModel(MotionControl.Control.Services.MagazineEventRecord record)
    {
        Timestamp = record.Timestamp;
        MagazineName = record.MagazineName;
        EventType = record.EventType;
        Message = record.Message;
    }

    public DateTime Timestamp { get; }
    public string MagazineName { get; }
    public string EventType { get; }
    public string Message { get; }
}
