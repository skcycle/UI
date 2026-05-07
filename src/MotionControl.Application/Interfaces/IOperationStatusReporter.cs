namespace MotionControl.Application.Interfaces;

public interface IOperationStatusReporter
{
    void ReportStatus(string message);
}
