using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using MotionControl.Control.Services;
using MotionControl.Domain.Entities;
using MotionControl.Presentation.Commands;

namespace MotionControl.Presentation.ViewModels;

public sealed class IoPointViewModel : INotifyPropertyChanged
{
    private readonly IoPoint _ioPoint;
    private readonly IoControlService _ioControlService;
    private readonly CommandFeedbackRuntimeState _commandFeedbackRuntimeState;
    private readonly Func<bool> _canToggleOutput;
    private readonly Machine? _machine;
    private bool _value;

    public IoPointViewModel(IoPoint ioPoint, IoControlService ioControlService, CommandFeedbackRuntimeState commandFeedbackRuntimeState, Func<bool> canToggleOutput, Machine? machine = null)
    {
        _ioPoint = ioPoint;
        _ioControlService = ioControlService;
        _commandFeedbackRuntimeState = commandFeedbackRuntimeState;
        _canToggleOutput = canToggleOutput;
        _machine = machine;
        _value = ioPoint.Value;
        SetCommand = new RelayCommand(
            async () =>
            {
                var nextValue = !_ioPoint.Value;
                var actionName = nextValue ? "OutputOn" : "OutputOff";
                var pointName = string.IsNullOrWhiteSpace(Name) ? $"IO {Address}" : $"{Name}(IO {Address})";
                _commandFeedbackRuntimeState.AddStarted(actionName, message: $"{pointName} set to {(nextValue ? "ON" : "OFF")} started");

                var result = await _ioControlService.SetOutputAsync(Address, nextValue);
                if (result.Success)
                {
                    _machine?.ClearAlarm(GetOutputFailedAlarmCode());
                    _ioPoint.Update(nextValue);
                    _value = nextValue;
                    OnPropertyChanged(nameof(Value));
                    OnPropertyChanged(nameof(StatusBrush));
                    _commandFeedbackRuntimeState.AddSucceeded(actionName, message: $"{pointName} set to {(nextValue ? "ON" : "OFF")} completed");
                }
                else
                {
                    var message = $"{pointName} set to {(nextValue ? "ON" : "OFF")} failed: {result.ErrorMessage}";
                    _machine?.UpsertAlarm(GetOutputFailedAlarmCode(), message, Name, "IO", "Error");
                    _commandFeedbackRuntimeState.AddFailed(actionName, message: message);
                }
            },
            () => IsOutput && _canToggleOutput());
    }

    public string Name
    {
        get => _ioPoint.Name;
        set
        {
            if (_ioPoint.Name == value) return;
            _ioPoint.UpdateMetadata(value, _ioPoint.Address, _ioPoint.Description);
            OnPropertyChanged();
        }
    }

    public int Address
    {
        get => _ioPoint.Address;
        set
        {
            if (_ioPoint.Address == value) return;
            _ioPoint.UpdateMetadata(_ioPoint.Name, value, _ioPoint.Description);
            OnPropertyChanged();
        }
    }

    public bool IsOutput => _ioPoint.IsOutput;
    public string Direction => _ioPoint.IsOutput ? "DO" : "DI";
    public string Description
    {
        get => _ioPoint.Description;
        set
        {
            if (_ioPoint.Description == value) return;
            _ioPoint.UpdateMetadata(_ioPoint.Name, _ioPoint.Address, value);
            OnPropertyChanged();
        }
    }

    public bool Value => _value;

    public Brush StatusBrush => _value
        ? new SolidColorBrush(Color.FromRgb(0, 200, 0))   // 绿灯 - ON
        : new SolidColorBrush(Color.FromRgb(100, 100, 100)); // 灰灯 - OFF

    public ICommand SetCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshCommandState()
    {
        (SetCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Refresh()
    {
        var newValue = _ioPoint.Value;
        _value = newValue;
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Address));
        OnPropertyChanged(nameof(Description));
    }

    private string GetOutputFailedAlarmCode()
    {
        var normalizedName = string.IsNullOrWhiteSpace(Name)
            ? $"ADDR-{Address}"
            : new string(Name.Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '-').ToArray());
        return $"IO-{normalizedName}-{Address}-WRITE-FAILED";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
