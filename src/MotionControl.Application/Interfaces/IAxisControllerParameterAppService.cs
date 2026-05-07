namespace MotionControl.Application.Interfaces;

public interface IAxisControllerParameterAppService
{
    Task<string> ReadControllerParametersAsync(int axisNo, CancellationToken cancellationToken = default);
    Task WriteControllerParametersAsync(int axisNo, double workVelocity, double setupVelocity, double pulseEquivalent, CancellationToken cancellationToken = default);
}
