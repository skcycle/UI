using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MotionControl.Control.Services;
using MotionControl.Device.Abstractions.Results;
using MotionControl.Domain.Entities;
using MotionControl.Presentation.Commands;
using MotionControl.Presentation.Dialogs;

namespace MotionControl.Presentation.ViewModels;

public sealed class AxisViewModel : INotifyPropertyChanged
{
    private readonly Axis _axis;
    private readonly Machine _machine;
    private readonly MotionControl.Control.Interfaces.IAxisControlService _axisControlService;
    private readonly IDialogService _dialogService;
    private readonly CommandFeedbackRuntimeState _commandFeedbackRuntimeState;

    public AxisViewModel(Axis axis, Machine machine, MotionControl.Control.Interfaces.IAxisControlService axisControlService, IDialogService dialogService, CommandFeedbackRuntimeState commandFeedbackRuntimeState)
    {
        _axis = axis;
        _machine = machine;
        _axisControlService = axisControlService;
        _dialogService = dialogService;
        _commandFeedbackRuntimeState = commandFeedbackRuntimeState;
        ClearAlarmCommand = new RelayCommand(async () => await ClearAlarmAsync(), () => HasAlarm);
        ToggleServoCommand = new RelayCommand(async () => await ToggleServoAsync());
    }

    public string Name => _axis.Name;
    public int AxisNo => _axis.Id.Value;
    public double CurrentPosition => _axis.CurrentPosition;
    public double EncoderPosition => _axis.EncoderPosition;
    public double CurrentVelocity => _axis.CurrentVelocity;
    public bool HasAlarm => _axis.HasAlarm;
    public bool IsHomed => _axis.IsHomed;
    public bool IsServoOn => _axis.ServoState == Domain.Enums.ServoState.On;
    public ICommand ToggleServoCommand { get; }
    public bool PositiveSoftLimitTriggered => _axis.PositiveSoftLimitTriggered;
    public bool NegativeSoftLimitTriggered => _axis.NegativeSoftLimitTriggered;
    public bool PositiveHardLimitTriggered => _axis.PositiveLimitTriggered;
    public bool NegativeHardLimitTriggered => _axis.NegativeLimitTriggered;
    public string HomeMode => _axis.HomeMode.ToString();
    public string ServoBinding => _axis.ServoBinding;
    public double WorkVelocity => _axis.WorkVelocity;
    public double SetupVelocity => _axis.SetupVelocity;
    public double PulseEquivalent => _axis.PulseEquivalent;
    public double CommandPositionMm => PulseEquivalent <= 0 ? 0 : CurrentPosition / PulseEquivalent;
    public double EncoderPositionMm => PulseEquivalent <= 0 ? 0 : EncoderPosition / PulseEquivalent;
    public string SoftLimitDisplay => _axis.SoftLimit is null ? "N/A" : $"{_axis.SoftLimit.Minimum} ~ {_axis.SoftLimit.Maximum}";
    public ICommand ClearAlarmCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh()
    {
        OnPropertyChanged(nameof(CurrentPosition));
        OnPropertyChanged(nameof(EncoderPosition));
        OnPropertyChanged(nameof(CurrentVelocity));
        OnPropertyChanged(nameof(HasAlarm));
        OnPropertyChanged(nameof(IsHomed));
        OnPropertyChanged(nameof(IsServoOn));
        OnPropertyChanged(nameof(PositiveSoftLimitTriggered));
        OnPropertyChanged(nameof(NegativeSoftLimitTriggered));
        OnPropertyChanged(nameof(PositiveHardLimitTriggered));
        OnPropertyChanged(nameof(NegativeHardLimitTriggered));
        OnPropertyChanged(nameof(WorkVelocity));
        OnPropertyChanged(nameof(SetupVelocity));
        OnPropertyChanged(nameof(PulseEquivalent));
        OnPropertyChanged(nameof(CommandPositionMm));
        OnPropertyChanged(nameof(EncoderPositionMm));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(HomeMode));
        OnPropertyChanged(nameof(ServoBinding));
        (ClearAlarmCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleServoCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private async Task ClearAlarmAsync()
    {
        var actionName = "AxisClearAlarm";
        var messagePrefix = $"Axis {AxisNo}";
        _commandFeedbackRuntimeState.AddStarted(actionName, AxisNo, $"{messagePrefix} clear alarm started");

        try
        {
            var result = await _axisControlService.ResetAlarmAsync(_axis);
            if (!result.Success)
            {
                var message = result.ErrorMessage ?? "未知错误";
                _machine.UpsertAlarm("SYS-AXIS-CLEARALARM-FAILED", $"{messagePrefix} clear alarm failed: {message}", messagePrefix, "Axis", "Error");
                _commandFeedbackRuntimeState.AddFailed(actionName, AxisNo, $"{messagePrefix} clear alarm failed: {message}");
                _dialogService.ShowAlarm(message, "报警清除失败");
                return;
            }

            _commandFeedbackRuntimeState.AddSucceeded(actionName, AxisNo, $"{messagePrefix} clear alarm completed");
            Refresh();
        }
        catch (InvalidOperationException ex)
        {
            _machine.UpsertAlarm("SYS-AXIS-CLEARALARM-FAILED", $"{messagePrefix} clear alarm failed: {ex.Message}", messagePrefix, "Axis", "Error");
            _commandFeedbackRuntimeState.AddFailed(actionName, AxisNo, $"{messagePrefix} clear alarm failed: {ex.Message}");
            _dialogService.ShowAlarm(ex.Message, "报警清除失败");
        }
    }

    private async Task ToggleServoAsync()
    {
        var currentOn = false;
        var actionName = "AxisServoToggle";
        var messagePrefix = $"Axis {AxisNo}";

        try
        {
            currentOn = await _axisControlService.IsServoOnAsync(_axis);
            var operation = currentOn ? "disable" : "enable";
            _commandFeedbackRuntimeState.AddStarted(actionName, AxisNo, $"{messagePrefix} servo {operation} started");

            DeviceResult r;
            if (currentOn)
            {
                r = await _axisControlService.DisableAxisAsync(_axis);
            }
            else
            {
                r = await _axisControlService.EnableAxisAsync(_axis);
            }
            if (!r.Success)
            {
                var message = r.ErrorMessage ?? "未知错误";
                _machine.UpsertAlarm("SYS-AXIS-SERVO-FAILED", $"{messagePrefix} servo {operation} failed: {message}", messagePrefix, "Axis", "Error");
                _commandFeedbackRuntimeState.AddFailed(actionName, AxisNo, $"{messagePrefix} servo {operation} failed: {message}");
                _dialogService.ShowAlarm(message, "伺服切换失败");
                return;
            }

            _commandFeedbackRuntimeState.AddSucceeded(actionName, AxisNo, $"{messagePrefix} servo {operation} completed");
            Refresh();
        }
        catch (InvalidOperationException ex)
        {
            var operation = currentOn ? "disable" : "enable";
            _machine.UpsertAlarm("SYS-AXIS-SERVO-FAILED", $"{messagePrefix} servo {operation} failed: {ex.Message}", messagePrefix, "Axis", "Error");
            _commandFeedbackRuntimeState.AddFailed(actionName, AxisNo, $"{messagePrefix} servo {operation} failed: {ex.Message}");
            _dialogService.ShowAlarm(ex.Message, "伺服切换失败");
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
