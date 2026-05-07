namespace MotionControl.Domain.Entities;

public sealed class Alarm
{
    public Alarm(string code, string message, DateTime occurredAt, string source = "System", string category = "General", string severity = "Error")
    {
        Code = code;
        Message = message;
        OccurredAt = occurredAt;
        Source = source;
        Category = category;
        Severity = severity;
    }

    public string Code { get; }
    public string Message { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string Source { get; private set; }
    public string Category { get; private set; }
    public string Severity { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(string message, DateTime occurredAt, string source, string category, string severity)
    {
        Message = message;
        OccurredAt = occurredAt;
        Source = source;
        Category = category;
        Severity = severity;
        IsActive = true;
    }

    public void Clear() => IsActive = false;
}
