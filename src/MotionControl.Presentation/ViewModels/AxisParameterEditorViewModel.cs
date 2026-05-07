using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Application.Interfaces;
using MotionControl.Control.Services;
using MotionControl.Domain.Enums;
using MotionControl.Infrastructure.Configuration;
using MotionControl.Presentation.Commands;
using MotionControl.Presentation.Dialogs;

namespace MotionControl.Presentation.ViewModels;

public sealed class AxisParameterEditorViewModel : INotifyPropertyChanged
{
    private readonly IAxisManagementAppService _axisManagementAppService;
    private readonly IAxisControllerParameterAppService _axisControllerParameterAppService;
    private readonly Func<bool> _canWriteParameters;
    private readonly Func<bool> _canAccessControllerParameters;
    private readonly IOperationStatusReporter _statusReporter;
    private readonly IDialogService _dialogService;
    private readonly CommandFeedbackRuntimeState _commandFeedbackRuntimeState;
    private int _axisNo;
    private string _name = string.Empty;
    private string _group = string.Empty;
    private bool _isMaster;
    private string _masterAxisName = string.Empty;
    private double? _softLimitPositive;
    private double? _softLimitNegative;
    private double? _workVelocity;
    private double? _setupVelocity;
    private double? _acceleration = 100;
    private double? _deceleration = 100;
    private double? _pulseEquivalent = 1000;
    private HomeMode _homeMode = HomeMode.Default;
    private string _servoBinding = string.Empty;
    private string _controllerParameterSnapshot = string.Empty;

    public AxisParameterEditorViewModel(
        IAxisManagementAppService axisManagementAppService,
        IAxisControllerParameterAppService axisControllerParameterAppService,
        Func<bool> canWriteParameters,
        Func<bool> canAccessControllerParameters,
        IOperationStatusReporter statusReporter,
        IDialogService dialogService,
        CommandFeedbackRuntimeState commandFeedbackRuntimeState)
    {
        _axisManagementAppService = axisManagementAppService;
        _axisControllerParameterAppService = axisControllerParameterAppService;
        _canWriteParameters = canWriteParameters;
        _canAccessControllerParameters = canAccessControllerParameters;
        _statusReporter = statusReporter;
        _dialogService = dialogService;
        _commandFeedbackRuntimeState = commandFeedbackRuntimeState;
        LoadCommand = new RelayCommand(async () => await LoadAsync(), () => AxisNo >= 0);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => AxisNo >= 0 && _canWriteParameters());
        ReadControllerCommand = new RelayCommand(async () => await ReadControllerAsync(), () => AxisNo >= 0 && _canAccessControllerParameters());
        WriteControllerCommand = new RelayCommand(async () => await WriteControllerAsync(), () => AxisNo >= 0 && _canAccessControllerParameters());
    }

    public int AxisNo
    {
        get => _axisNo;
        set
        {
            _axisNo = value;
            OnPropertyChanged();
            LoadCommand.RaiseCanExecuteChanged();
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Group
    {
        get => _group;
        set { _group = value; OnPropertyChanged(); }
    }

    public bool IsMaster
    {
        get => _isMaster;
        set { _isMaster = value; OnPropertyChanged(); }
    }

    public string MasterAxisName
    {
        get => _masterAxisName;
        set { _masterAxisName = value; OnPropertyChanged(); }
    }

    public double? SoftLimitPositive
    {
        get => _softLimitPositive;
        set { _softLimitPositive = value; OnPropertyChanged(); }
    }

    public double? SoftLimitNegative
    {
        get => _softLimitNegative;
        set { _softLimitNegative = value; OnPropertyChanged(); }
    }

    public double? WorkVelocity
    {
        get => _workVelocity;
        set { _workVelocity = value; OnPropertyChanged(); }
    }

    public double? SetupVelocity
    {
        get => _setupVelocity;
        set { _setupVelocity = value; OnPropertyChanged(); }
    }

    public double? Acceleration
    {
        get => _acceleration;
        set { _acceleration = value; OnPropertyChanged(); }
    }

    public double? Deceleration
    {
        get => _deceleration;
        set { _deceleration = value; OnPropertyChanged(); }
    }

    public double? PulseEquivalent
    {
        get => _pulseEquivalent;
        set { _pulseEquivalent = value; OnPropertyChanged(); }
    }

    public HomeMode HomeMode
    {
        get => _homeMode;
        set { _homeMode = value; OnPropertyChanged(); }
    }

    public string ServoBinding
    {
        get => _servoBinding;
        set { _servoBinding = value; OnPropertyChanged(); }
    }

    public string ControllerParameterSnapshot
    {
        get => _controllerParameterSnapshot;
        set { _controllerParameterSnapshot = value; OnPropertyChanged(); }
    }

    public Array HomeModes => Enum.GetValues(typeof(HomeMode));
    public RelayCommand LoadCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ReadControllerCommand { get; }
    public RelayCommand WriteControllerCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshCommandStates()
    {
        LoadCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        ReadControllerCommand.RaiseCanExecuteChanged();
        WriteControllerCommand.RaiseCanExecuteChanged();
    }

    public async Task SyncAxisNoAsync(int axisNo)
    {
        AxisNo = axisNo;
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        _commandFeedbackRuntimeState.AddStarted("AxisConfigLoad", AxisNo, $"Axis {AxisNo} config load started");

        try
        {
            var item = await _axisManagementAppService.LoadAxisAsync(AxisNo);
            if (item is null)
            {
                var notFoundMessage = $"Axis {AxisNo} config not found";
                _statusReporter.ReportStatus(notFoundMessage);
                _commandFeedbackRuntimeState.AddFailed("AxisConfigLoad", AxisNo, notFoundMessage);
                return;
            }

            Name = item.Name;
            Group = item.Group;
            IsMaster = item.IsMaster;
            MasterAxisName = item.MasterAxisName ?? string.Empty;
            SoftLimitPositive = item.SoftLimitPositive;
            SoftLimitNegative = item.SoftLimitNegative;
            WorkVelocity = item.WorkVelocity;
            SetupVelocity = item.SetupVelocity;
            Acceleration = item.Acceleration ?? 100;
            Deceleration = item.Deceleration ?? 100;
            PulseEquivalent = item.PulseEquivalent ?? 1000;
            HomeMode = item.HomeMode;
            ServoBinding = item.ServoBinding;

            var successMessage = $"Axis {AxisNo} config loaded";
            _statusReporter.ReportStatus(successMessage);
            _commandFeedbackRuntimeState.AddSucceeded("AxisConfigLoad", AxisNo, successMessage);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Axis {AxisNo} config load failed: {ex.Message}";
            _statusReporter.ReportStatus(errorMessage);
            _commandFeedbackRuntimeState.AddFailed("AxisConfigLoad", AxisNo, errorMessage);
        }
    }

    public async Task SaveAsync()
    {
        if (!TryValidate(out var validationMessage))
        {
            var message = $"Axis 参数校验失败: {validationMessage}";
            _statusReporter.ReportStatus(message);
            _commandFeedbackRuntimeState.AddFailed("AxisConfigSave", AxisNo, message);
            _dialogService.ShowWarning(validationMessage, "参数校验失败");
            return;
        }

        var item = new AxisMappingItem
        {
            AxisNo = AxisNo,
            Name = Name,
            Group = Group,
            IsMaster = IsMaster,
            MasterAxisName = string.IsNullOrWhiteSpace(MasterAxisName) ? null : MasterAxisName,
            SoftLimitPositive = SoftLimitPositive,
            SoftLimitNegative = SoftLimitNegative,
            WorkVelocity = WorkVelocity,
            SetupVelocity = SetupVelocity,
            Acceleration = Acceleration,
            Deceleration = Deceleration,
            PulseEquivalent = PulseEquivalent,
            HomeMode = HomeMode,
            ServoBinding = ServoBinding
        };

        _commandFeedbackRuntimeState.AddStarted("AxisConfigSave", AxisNo, $"Axis {AxisNo} config save started");

        try
        {
            await _axisManagementAppService.SaveAxisAsync(item);
            var message = $"Axis {AxisNo} config saved";
            _statusReporter.ReportStatus(message);
            _commandFeedbackRuntimeState.AddSucceeded("AxisConfigSave", AxisNo, message);
        }
        catch (InvalidOperationException ex)
        {
            var message = $"Axis {AxisNo} 保存失败: {ex.Message}";
            _statusReporter.ReportStatus(message);
            _commandFeedbackRuntimeState.AddFailed("AxisConfigSave", AxisNo, message);
        }
    }

    public async Task ReadControllerAsync()
    {
        _commandFeedbackRuntimeState.AddStarted("AxisControllerRead", AxisNo, $"Axis {AxisNo} controller parameter read started");

        try
        {
            ControllerParameterSnapshot = await _axisControllerParameterAppService.ReadControllerParametersAsync(AxisNo);
            var message = $"Axis {AxisNo} controller parameters read";
            _statusReporter.ReportStatus(message);
            _commandFeedbackRuntimeState.AddSucceeded("AxisControllerRead", AxisNo, message);
        }
        catch (Exception ex)
        {
            var message = $"Axis {AxisNo} controller parameter read failed: {ex.Message}";
            _statusReporter.ReportStatus(message);
            _commandFeedbackRuntimeState.AddFailed("AxisControllerRead", AxisNo, message);
        }
    }

    public async Task WriteControllerAsync()
    {
        _commandFeedbackRuntimeState.AddStarted("AxisControllerWrite", AxisNo, $"Axis {AxisNo} controller parameter write started");

        try
        {
            await _axisControllerParameterAppService.WriteControllerParametersAsync(
                AxisNo,
                WorkVelocity ?? 0,
                SetupVelocity ?? 0,
                PulseEquivalent ?? 1000);
            var message = $"Axis {AxisNo} controller parameters written";
            _statusReporter.ReportStatus(message);
            _commandFeedbackRuntimeState.AddSucceeded("AxisControllerWrite", AxisNo, message);
        }
        catch (Exception ex)
        {
            var message = $"Axis {AxisNo} controller parameter write failed: {ex.Message}";
            _statusReporter.ReportStatus(message);
            _commandFeedbackRuntimeState.AddFailed("AxisControllerWrite", AxisNo, message);
        }
    }

    private bool TryValidate(out string validationMessage)
    {
        if (AxisNo < 0)
        {
            validationMessage = "AxisNo 不能小于 0";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            validationMessage = "Axis 名称不能为空";
            return false;
        }

        if (WorkVelocity.HasValue && WorkVelocity.Value <= 0)
        {
            validationMessage = "WorkVelocity 必须大于 0";
            return false;
        }

        if (SetupVelocity.HasValue && SetupVelocity.Value <= 0)
        {
            validationMessage = "SetupVelocity 必须大于 0";
            return false;
        }

        if (Acceleration.HasValue && Acceleration.Value <= 0)
        {
            validationMessage = "Acceleration 必须大于 0";
            return false;
        }

        if (Deceleration.HasValue && Deceleration.Value <= 0)
        {
            validationMessage = "Deceleration 必须大于 0";
            return false;
        }

        if (PulseEquivalent.HasValue && PulseEquivalent.Value <= 0)
        {
            validationMessage = "PulseEquivalent 必须大于 0";
            return false;
        }

        if (SoftLimitPositive.HasValue && SoftLimitNegative.HasValue && SoftLimitPositive.Value <= SoftLimitNegative.Value)
        {
            validationMessage = "SoftLimitPositive 必须大于 SoftLimitNegative";
            return false;
        }

        validationMessage = string.Empty;
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
