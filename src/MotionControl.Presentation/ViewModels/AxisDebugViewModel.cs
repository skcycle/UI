using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Application.DTOs;
using MotionControl.Application.Interfaces;
using MotionControl.Control.Services;
using MotionControl.Device.Abstractions.Results;
using MotionControl.Domain.Entities;
using MotionControl.Presentation.Commands;

namespace MotionControl.Presentation.ViewModels;

public sealed class AxisDebugViewModel : INotifyPropertyChanged
{
    private readonly IMotionAppService _motionAppService;
    private readonly Machine _machine;
    private readonly HomePlanRuntimeState _homePlanRuntimeState;
    private readonly CommandFeedbackRuntimeState _commandFeedbackRuntimeState;
    private readonly Func<bool> _canControlAxis;
    private int _selectedAxisNo;
    private double _targetPosition;
    private double _velocity = 100;
    private double _acceleration = 100;
    private double _deceleration = 100;
    private double _jogStepDistance = 1;

    public AxisDebugViewModel(IMotionAppService motionAppService, Machine machine, HomePlanRuntimeState homePlanRuntimeState, CommandFeedbackRuntimeState commandFeedbackRuntimeState, Func<bool> canControlAxis)
    {
        _motionAppService = motionAppService;
        _machine = machine;
        _homePlanRuntimeState = homePlanRuntimeState;
        _commandFeedbackRuntimeState = commandFeedbackRuntimeState;
        _canControlAxis = canControlAxis;

        EnableAxisCommand = new RelayCommand(async () => await EnableSelectedAxisAsync(), CanExecuteAxisCommand);
        DisableAxisCommand = new RelayCommand(async () => await DisableSelectedAxisAsync(), CanExecuteAxisCommand);
        HomeAxisCommand = new RelayCommand(async () => await HomeSelectedAxisAsync(), CanExecuteAxisCommand);
        MoveAxisCommand = new RelayCommand(async () => await MoveSelectedAxisAsync(), CanExecuteAxisCommand);
        StopAxisCommand = new RelayCommand(async () => await StopSelectedAxisAsync(), CanExecuteAxisCommand);
        JogPositiveCommand = new RelayCommand(async () => await StartJogAsync(true), CanExecuteAxisCommand);
        JogNegativeCommand = new RelayCommand(async () => await StartJogAsync(false), CanExecuteAxisCommand);
    }

    public event Action<int>? SelectedAxisChanged;

    public int SelectedAxisNo
    {
        get => _selectedAxisNo;
        set
        {
            if (_selectedAxisNo == value)
            {
                return;
            }

            _selectedAxisNo = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedAxisHomeMode));
            OnPropertyChanged(nameof(SelectedAxisServoBinding));
            OnPropertyChanged(nameof(SelectedAxisSoftLimit));
            OnPropertyChanged(nameof(SelectedHomePlanTitle));
            OnPropertyChanged(nameof(SelectedHomePlanSteps));
            EnableAxisCommand.RaiseCanExecuteChanged();
            HomeAxisCommand.RaiseCanExecuteChanged();
            MoveAxisCommand.RaiseCanExecuteChanged();
            StopAxisCommand.RaiseCanExecuteChanged();
            JogPositiveCommand.RaiseCanExecuteChanged();
            JogNegativeCommand.RaiseCanExecuteChanged();
            SelectedAxisChanged?.Invoke(_selectedAxisNo);
        }
    }

    public void RefreshCommandStates()
    {
        EnableAxisCommand.RaiseCanExecuteChanged();
        HomeAxisCommand.RaiseCanExecuteChanged();
        MoveAxisCommand.RaiseCanExecuteChanged();
        StopAxisCommand.RaiseCanExecuteChanged();
        JogPositiveCommand.RaiseCanExecuteChanged();
        JogNegativeCommand.RaiseCanExecuteChanged();
    }

    public double TargetPosition
    {
        get => _targetPosition;
        set
        {
            _targetPosition = value;
            OnPropertyChanged();
        }
    }

    public double Velocity
    {
        get => _velocity;
        set
        {
            _velocity = value;
            OnPropertyChanged();
        }
    }

    public double Acceleration
    {
        get => _acceleration;
        set
        {
            _acceleration = value;
            OnPropertyChanged();
        }
    }

    public double Deceleration
    {
        get => _deceleration;
        set
        {
            _deceleration = value;
            OnPropertyChanged();
        }
    }

    public double JogStepDistance
    {
        get => _jogStepDistance;
        set
        {
            if (_jogStepDistance == value)
            {
                return;
            }

            _jogStepDistance = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<double> JogStepDistances { get; } = new[] { 0.1d, 1d, 10d };

    public string SelectedAxisHomeMode => SelectedAxis?.HomeMode.ToString() ?? "N/A";
    public string SelectedAxisServoBinding => SelectedAxis?.ServoBinding ?? "N/A";
    public string SelectedAxisSoftLimit => SelectedAxis?.SoftLimit is null ? "N/A" : $"{SelectedAxis.SoftLimit.Minimum} ~ {SelectedAxis.SoftLimit.Maximum}";
    public string SelectedHomePlanTitle => _homePlanRuntimeState.CurrentPlan?.Title ?? "No plan generated";
    public IReadOnlyList<string> SelectedHomePlanSteps => _homePlanRuntimeState.CurrentPlan?.Steps ?? Array.Empty<string>();
    public RelayCommand EnableAxisCommand { get; }
    public RelayCommand DisableAxisCommand { get; }
    public RelayCommand HomeAxisCommand { get; }
    public RelayCommand MoveAxisCommand { get; }
    public RelayCommand StopAxisCommand { get; }
    public RelayCommand JogPositiveCommand { get; }
    public RelayCommand JogNegativeCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private Axis? SelectedAxis => _machine.Axes.FirstOrDefault(axis => axis.Id.Value == SelectedAxisNo);

    private bool CanExecuteAxisCommand() => SelectedAxis is not null && _canControlAxis();

    public async Task EnableSelectedAxisAsync()
    {
        var messagePrefix = $"Axis {SelectedAxisNo}";
        _commandFeedbackRuntimeState.AddStarted("AxisEnable", SelectedAxisNo, $"{messagePrefix} enable started");
        var r = await _motionAppService.EnableAxisAsync(new AxisCommandDto(SelectedAxisNo));
        if (r.Success)
        {
            _commandFeedbackRuntimeState.AddSucceeded("AxisEnable", SelectedAxisNo, $"{messagePrefix} enable completed");
        }
        else
        {
            var message = $"{messagePrefix} enable failed: {r.ErrorMessage}";
            _machine.UpsertAlarm("SYS-AXIS-COMMAND-FAILED", message, messagePrefix, "Axis", "Error");
            _commandFeedbackRuntimeState.AddFailed("AxisEnable", SelectedAxisNo, message);
        }
    }

    public async Task DisableSelectedAxisAsync()
    {
        var messagePrefix = $"Axis {SelectedAxisNo}";
        _commandFeedbackRuntimeState.AddStarted("AxisDisable", SelectedAxisNo, $"{messagePrefix} disable started");
        var r = await _motionAppService.DisableAxisAsync(new AxisCommandDto(SelectedAxisNo));
        if (r.Success)
        {
            _commandFeedbackRuntimeState.AddSucceeded("AxisDisable", SelectedAxisNo, $"{messagePrefix} disable completed");
        }
        else
        {
            var message = $"{messagePrefix} disable failed: {r.ErrorMessage}";
            _machine.UpsertAlarm("SYS-AXIS-COMMAND-FAILED", message, messagePrefix, "Axis", "Error");
            _commandFeedbackRuntimeState.AddFailed("AxisDisable", SelectedAxisNo, message);
        }
    }

    public async Task HomeSelectedAxisAsync()
    {
        var messagePrefix = $"Axis {SelectedAxisNo}";
        _commandFeedbackRuntimeState.AddStarted("AxisHome", SelectedAxisNo, $"{messagePrefix} home started");
        var r = await _motionAppService.HomeAxisAsync(new AxisCommandDto(SelectedAxisNo));
        if (r.Success)
        {
            _commandFeedbackRuntimeState.AddSucceeded("AxisHome", SelectedAxisNo, $"{messagePrefix} home completed");
        }
        else
        {
            var message = $"{messagePrefix} home failed: {r.ErrorMessage}";
            _machine.UpsertAlarm("SYS-AXIS-COMMAND-FAILED", message, messagePrefix, "Axis", "Error");
            _commandFeedbackRuntimeState.AddFailed("AxisHome", SelectedAxisNo, message);
        }
    }

    public async Task MoveSelectedAxisAsync()
    {
        var messagePrefix = $"Axis {SelectedAxisNo}";
        _commandFeedbackRuntimeState.AddStarted("AxisMove", SelectedAxisNo, $"{messagePrefix} move started");
        var r = await _motionAppService.MoveAbsoluteAsync(new MoveAxisCommandDto(SelectedAxisNo, TargetPosition, Velocity, Acceleration, Deceleration));
        if (r.Success)
        {
            _commandFeedbackRuntimeState.AddSucceeded("AxisMove", SelectedAxisNo, $"{messagePrefix} move completed");
        }
        else
        {
            var message = $"{messagePrefix} move failed: {r.ErrorMessage}";
            _machine.UpsertAlarm("SYS-AXIS-COMMAND-FAILED", message, messagePrefix, "Axis", "Error");
            _commandFeedbackRuntimeState.AddFailed("AxisMove", SelectedAxisNo, message);
        }
    }

    public async Task StopSelectedAxisAsync()
    {
        var messagePrefix = $"Axis {SelectedAxisNo}";
        _commandFeedbackRuntimeState.AddStarted("AxisStop", SelectedAxisNo, $"{messagePrefix} stop started");
        var r = await _motionAppService.StopAxisAsync(new AxisCommandDto(SelectedAxisNo));
        if (r.Success)
        {
            _commandFeedbackRuntimeState.AddSucceeded("AxisStop", SelectedAxisNo, $"{messagePrefix} stop completed");
        }
        else
        {
            var message = $"{messagePrefix} stop failed: {r.ErrorMessage}";
            _machine.UpsertAlarm("SYS-AXIS-COMMAND-FAILED", message, messagePrefix, "Axis", "Error");
            _commandFeedbackRuntimeState.AddFailed("AxisStop", SelectedAxisNo, message);
        }
    }

    public async Task StartJogAsync(bool positiveDirection)
    {
        if (SelectedAxis is null)
        {
            return;
        }

        var direction = positiveDirection ? "positive" : "negative";
        var messagePrefix = $"Axis {SelectedAxisNo}";
        _commandFeedbackRuntimeState.AddStarted("AxisJog", SelectedAxisNo, $"{messagePrefix} jog {direction} started");
        var r = await _motionAppService.JogAxisAsync(new JogAxisCommandDto(SelectedAxisNo, Velocity, positiveDirection));
        if (r.Success)
        {
            _commandFeedbackRuntimeState.AddSucceeded("AxisJog", SelectedAxisNo, $"{messagePrefix} jog {direction} started");
        }
        else
        {
            var message = $"{messagePrefix} jog {direction} failed: {r.ErrorMessage}";
            _machine.UpsertAlarm("SYS-AXIS-COMMAND-FAILED", message, messagePrefix, "Axis", "Error");
            _commandFeedbackRuntimeState.AddFailed("AxisJog", SelectedAxisNo, message);
        }
    }

    public async Task StopJogAsync()
    {
        if (SelectedAxis is null)
        {
            return;
        }

        var messagePrefix = $"Axis {SelectedAxisNo}";
        _commandFeedbackRuntimeState.AddStarted("AxisJogStop", SelectedAxisNo, $"{messagePrefix} jog stop started");
        var r = await _motionAppService.StopAxisAsync(new AxisCommandDto(SelectedAxisNo));
        if (r.Success)
        {
            _commandFeedbackRuntimeState.AddSucceeded("AxisJogStop", SelectedAxisNo, $"{messagePrefix} jog stop completed");
        }
        else
        {
            var message = $"{messagePrefix} jog stop failed: {r.ErrorMessage}";
            _machine.UpsertAlarm("SYS-AXIS-COMMAND-FAILED", message, messagePrefix, "Axis", "Error");
            _commandFeedbackRuntimeState.AddFailed("AxisJogStop", SelectedAxisNo, message);
        }
    }

    public async Task StepMoveAsync(bool positiveDirection)
    {
        if (SelectedAxis is null)
        {
            return;
        }

        var pulseEquivalent = SelectedAxis.PulseEquivalent > 0 ? SelectedAxis.PulseEquivalent : 1000;
        var currentPositionMm = SelectedAxis.CurrentPosition / pulseEquivalent;
        var step = positiveDirection ? JogStepDistance : -JogStepDistance;
        var targetPositionMm = currentPositionMm + step;
        var direction = positiveDirection ? "positive" : "negative";
        var messagePrefix = $"Axis {SelectedAxisNo}";
        _commandFeedbackRuntimeState.AddStarted("AxisStepMove", SelectedAxisNo, $"{messagePrefix} step move {direction} started");
        var result = await _motionAppService.MoveAbsoluteAsync(new MoveAxisCommandDto(SelectedAxisNo, targetPositionMm, Velocity, Acceleration, Deceleration));
        if (result.Success)
        {
            TargetPosition = targetPositionMm;
            _commandFeedbackRuntimeState.AddSucceeded("AxisStepMove", SelectedAxisNo, $"{messagePrefix} step move {direction} completed");
        }
        else
        {
            var message = $"{messagePrefix} step move {direction} failed: {result.ErrorMessage}";
            _machine.UpsertAlarm("SYS-AXIS-COMMAND-FAILED", message, messagePrefix, "Axis", "Error");
            _commandFeedbackRuntimeState.AddFailed("AxisStepMove", SelectedAxisNo, message);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
