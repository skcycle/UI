using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MotionControl.Domain.Enums;
using MotionControl.Application.Interfaces;
using MotionControl.Control.Services;
using MotionControl.Presentation.Dialogs;
using MotionControl.Device.Abstractions.Results;
using MotionControl.Infrastructure.Configuration;
using MotionControl.Domain.Entities;
using MotionControl.Domain.ValueObjects;
using MotionControl.Presentation.Commands;

namespace MotionControl.Presentation.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IOperationStatusReporter
{
    public enum NavigationPage
    {
        Dashboard,
        EtherCat,
        Axis,
        Io,
        Cylinder,
        Magazine,
        WorkHead,
        PositionSetup,
        Alarm,
        EventLog
    }
    private readonly Machine _machine;
    private readonly IMotionAppService _motionAppService;
    private readonly IAxisManagementAppService _axisManagementAppService;
    private readonly IIoManagementAppService _ioManagementAppService;
    private readonly ICylinderManagementAppService _cylinderManagementAppService;
    private readonly IMagazineManagementAppService _magazineManagementAppService;
    private readonly IWorkHeadManagementAppService _workHeadManagementAppService;
    private readonly IPositionSetupManagementAppService _positionSetupManagementAppService;
    private readonly ISystemAppService _systemAppService;
    private readonly IDialogService _dialogService;
    private readonly AxisConsoleCoordinator _axisConsoleCoordinator;
    private readonly IoMonitorCoordinator _ioMonitorCoordinator;
    private readonly CommandFeedbackRuntimeState _commandFeedbackRuntimeState;
    private readonly CylinderEventRuntimeState _cylinderEventRuntimeState;
    private readonly MagazineEventRuntimeState _magazineEventRuntimeState;
    private readonly PositionSetupEventRuntimeState _positionSetupEventRuntimeState;
    private readonly WorkHeadEventRuntimeState _workHeadEventRuntimeState;
    private readonly ControllerRuntimeState _controllerRuntimeState;
    private readonly Device.Abstractions.Controllers.IAxisMotionController _motionController;
    private readonly Timer _clockTimer;
    private DateTime _lastDashboardRefreshUtc = DateTime.MinValue;
    private DateTime _lastAxisRefreshUtc = DateTime.MinValue;
    private DateTime _lastAlarmRefreshUtc = DateTime.MinValue;
    private DateTime _lastIoRefreshUtc = DateTime.MinValue;
    private DateTime _lastIoEventRefreshUtc = DateTime.MinValue;
    private DateTime _lastCylinderRefreshUtc = DateTime.MinValue;
    private DateTime _lastMagazineRefreshUtc = DateTime.MinValue;
    private DateTime _lastWorkHeadRefreshUtc = DateTime.MinValue;
    private DateTime _lastPositionSetupRefreshUtc = DateTime.MinValue;
    private string _currentBeijingTime = GetBeijingTimeString();
    private string _operationStatus = "Ready";
    private NavigationPage _selectedPage = NavigationPage.Dashboard;
    private string? _selectedWorkHeadMotionName;
    private double _workHeadTargetX;
    private double _workHeadTargetY;
    private double _workHeadTargetZ;
    private double _workHeadTargetR;
    private double _workHeadMoveVelocity = 100;
    private double _workHeadMoveAcceleration = 100;
    private double _workHeadMoveDeceleration = 100;
    private string? _selectedWorkHeadPositionName;

    public MainWindowViewModel(
        Machine machine,
        ISystemAppService systemAppService,
        IDialogService dialogService,
        IMotionAppService motionAppService,
        IAxisManagementAppService axisManagementAppService,
        IAxisControllerParameterAppService axisControllerParameterAppService,
        MotionControl.Control.Interfaces.IAxisControlService axisControlService,
        IIoManagementAppService ioManagementAppService,
        ICylinderManagementAppService cylinderManagementAppService,
        IMagazineManagementAppService magazineManagementAppService,
        IWorkHeadManagementAppService workHeadManagementAppService,
        IPositionSetupManagementAppService positionSetupManagementAppService,
        ControllerRuntimeState controllerRuntimeState,
        Device.Abstractions.Controllers.IAxisMotionController motionController,
        HomePlanRuntimeState homePlanRuntimeState,
        CommandFeedbackRuntimeState commandFeedbackRuntimeState,
        IoEventRuntimeState ioEventRuntimeState,
        CylinderEventRuntimeState cylinderEventRuntimeState,
        MagazineEventRuntimeState magazineEventRuntimeState,
        WorkHeadEventRuntimeState workHeadEventRuntimeState,
        PositionSetupEventRuntimeState positionSetupEventRuntimeState,
        IoControlService ioControlService,
        EventLogQueryService eventLogQueryService)
    {
        _machine = machine;
        _motionAppService = motionAppService;
        _axisManagementAppService = axisManagementAppService;
        _ioManagementAppService = ioManagementAppService;
        _cylinderManagementAppService = cylinderManagementAppService;
        _magazineManagementAppService = magazineManagementAppService;
        _workHeadManagementAppService = workHeadManagementAppService;
        _positionSetupManagementAppService = positionSetupManagementAppService;
        _commandFeedbackRuntimeState = commandFeedbackRuntimeState;
        _cylinderEventRuntimeState = cylinderEventRuntimeState;
        _magazineEventRuntimeState = magazineEventRuntimeState;
        _workHeadEventRuntimeState = workHeadEventRuntimeState;
        _positionSetupEventRuntimeState = positionSetupEventRuntimeState;
        _commandFeedbackRuntimeState.FeedbackChanged += () =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                RefreshViewModels(force: true);
            }
            else
            {
                _ = dispatcher.BeginInvoke(() => RefreshViewModels(force: true));
            }
        };
        _systemAppService = systemAppService;
        _dialogService = dialogService;
        _controllerRuntimeState = controllerRuntimeState;
        _motionController = motionController;
        Dashboard = new DashboardViewModel(machine, commandFeedbackRuntimeState);
        EtherCatMonitor = new EtherCatMonitorViewModel(Dashboard);
        AxisMonitor = new AxisMonitorViewModel(machine, axisControlService, dialogService, commandFeedbackRuntimeState);
        AxisMonitor.SelectedAxisChanged += _ => RaiseAxisDeleteCanExecuteChanged();
        IoMonitor = new IoMonitorViewModel(machine, ioControlService, commandFeedbackRuntimeState, CanWriteIoOutputs);
        IoEventLog = new IoEventLogViewModel(ioEventRuntimeState);
        CylinderEventLog = new CylinderEventLogViewModel(cylinderEventRuntimeState);
        EventLogQuery = new RuntimeEventLogQueryViewModel(eventLogQueryService);
        MagazineEventLog = new MagazineEventLogViewModel(magazineEventRuntimeState);
        WorkHeadEventLog = new WorkHeadEventLogViewModel(workHeadEventRuntimeState);
        PositionSetupEventLog = new PositionSetupEventLogViewModel(positionSetupEventRuntimeState);
        CylinderMonitor = new CylinderMonitorViewModel(machine, ioControlService, cylinderEventRuntimeState, CanWriteIoOutputs);
        CylinderMonitor.SelectedCylinderChanged += _ => (DeleteCylinderCommand as RelayCommand)?.RaiseCanExecuteChanged();
        MagazineMonitor = new MagazineMonitorViewModel(machine);
        MagazineMonitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MagazineMonitorViewModel.SelectedMagazine))
            {
                (DeleteMagazineCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddMagazinePositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteMagazinePositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (TeachMagazinePositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveMagazinePositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ScanMagazineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        };
        MagazineMonitor.SelectedMagazinePositionChanged += () =>
        {
            (DeleteMagazinePositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TeachMagazinePositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveMagazinePositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ScanMagazineCommand as RelayCommand)?.RaiseCanExecuteChanged();
        };
        WorkHeadMonitor = new WorkHeadMonitorViewModel(machine, ioControlService, motionAppService, workHeadEventRuntimeState, CanWriteIoOutputs);
        WorkHeadMonitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkHeadMonitorViewModel.SelectedWorkHead))
            {
                (DeleteWorkHeadCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddWorkHeadPositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteWorkHeadPositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        };
        PositionSetupMonitor = new PositionSetupMonitorViewModel();
        PositionSetupMonitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PositionSetupMonitorViewModel.SelectedItem))
            {
                // 订阅新的 PositionSetupItemViewModel 的 HasSelectedPositionChanged
                if (PositionSetupMonitor.SelectedItem != null)
                {
                    PositionSetupMonitor.SelectedItem.HasSelectedPositionChanged += () =>
                    {
                        (DeletePositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                        (AddPositionSetupPositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                        (DeletePositionSetupPositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                        (TeachPositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                        (MovePositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    };
                }
                (DeletePositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddPositionSetupPositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeletePositionSetupPositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (TeachPositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MovePositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(PositionSetupMonitorViewModel.SelectedItem) + ".SelectedPosition")
            {
                (DeletePositionSetupPositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (TeachPositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MovePositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        };
        AxisDebug = new AxisDebugViewModel(motionAppService, machine, homePlanRuntimeState, commandFeedbackRuntimeState, CanControlAxisCommands);
        AxisParameterEditor = new AxisParameterEditorViewModel(
            axisManagementAppService,
            axisControllerParameterAppService,
            CanWriteAxisConfiguration,
            CanAccessControllerParameters,
            this,
            dialogService,
            commandFeedbackRuntimeState);
        AxisDataCapture = new AxisDataCaptureViewModel(machine, CanControlAxisCommands, motionController);
        AxisDebug.SelectedAxisChanged += axisNo => AxisDataCapture.SetSelectedAxisNo(axisNo);
        AxisDataCapture.RefreshCommandStates(); // ensure buttons are correct at startup
        Alarm = new AlarmViewModel(machine);
        EmergencyStopCommand = new RelayCommand(
            async () =>
            {
                try
                {
                    _commandFeedbackRuntimeState.AddStarted("EmergencyStop", message: "Emergency stop requested");
                    await _systemAppService.EmergencyStopAsync();
                    _commandFeedbackRuntimeState.AddSucceeded("EmergencyStop", message: "Emergency stop completed");
                }
                catch (Exception ex)
                {
                    var message = $"Emergency stop failed: {ex.Message}";
                    _commandFeedbackRuntimeState.AddFailed("EmergencyStop", message: message);
                    OperationStatus = message;
                }

                RefreshViewModels(force: true);
            });
        ClearEmergencyStopCommand = new RelayCommand(
            async () =>
            {
                try
                {
                    const string operatorName = "Operator";

                    // Step 1: 申请解锁急停
                    _commandFeedbackRuntimeState.AddStarted("ClearEmergencyStop", message: "Clear emergency stop requested");
                    var requestResult = await _systemAppService.RequestClearEmergencyStopAsync(operatorName);
                    if (!requestResult.Success)
                    {
                        OperationStatus = requestResult.Message;
                        _commandFeedbackRuntimeState.AddFailed("ClearEmergencyStop", message: requestResult.Message);
                        RefreshViewModels(force: true);
                        return;
                    }

                    // Step 2: 确认解锁
                    var clearResult = await _systemAppService.ClearEmergencyStopAsync(operatorName);
                    if (!clearResult.Success)
                    {
                        OperationStatus = clearResult.Message;
                        _commandFeedbackRuntimeState.AddFailed("ClearEmergencyStop", message: clearResult.Message);
                    }
                    else
                    {
                        _commandFeedbackRuntimeState.AddSucceeded("ClearEmergencyStop", message: clearResult.Message);
                        OperationStatus = clearResult.Message;
                    }
                }
                catch (Exception ex)
                {
                    var message = $"Clear emergency stop failed: {ex.Message}";
                    _commandFeedbackRuntimeState.AddFailed("ClearEmergencyStop", message: message);
                    OperationStatus = message;
                }

                RefreshViewModels(force: true);
            });
        ReconnectCommand = new RelayCommand(
            async () =>
            {
                try
                {
                    _commandFeedbackRuntimeState.AddStarted("Reconnect", message: "Controller reconnect started");
                    await _systemAppService.ReconnectAsync();

                    if (_controllerRuntimeState.IsConnected)
                    {
                        _commandFeedbackRuntimeState.AddSucceeded("Reconnect", message: "Controller reconnect completed");
                        AxisDataCapture.RefreshCommandStates();
                    }
                    else
                    {
                        const string reconnectFailedMessage = "Controller reconnect failed";
                        _machine.UpsertAlarm("SYS-CONTROLLER-RECONNECT-FAILED", reconnectFailedMessage, "System", "Communication", "Error");
                        _commandFeedbackRuntimeState.AddFailed("Reconnect", message: reconnectFailedMessage);
                    }
                }
                catch (Exception ex)
                {
                    var message = $"Controller reconnect failed: {ex.Message}";
                    _machine.UpsertAlarm("SYS-CONTROLLER-RECONNECT-FAILED", message, "System", "Communication", "Error");
                    _commandFeedbackRuntimeState.AddFailed("Reconnect", message: message);
                    OperationStatus = message;
                }

                RefreshViewModels(force: true);
            });
        AddAxisCommand = new RelayCommand(async () => await AddAxisAsync());
        DeleteAxisCommand = new RelayCommand(async () => await DeleteSelectedAxisAsync(), () => AxisMonitor.SelectedAxis is not null);
        SaveAxisConsoleConfigCommand = new RelayCommand(async () => await SaveAxisConsoleConfigAsync(), CanEditIoConfiguration);
        LoadAxisConsoleConfigCommand = new RelayCommand(async () => await LoadAxisConsoleConfigAsync(), CanEditIoConfiguration);
        AddInputCommand = new RelayCommand(async () => await AddIoPointAsync(false), CanEditIoConfiguration);
        AddOutputCommand = new RelayCommand(async () => await AddIoPointAsync(true), CanEditIoConfiguration);
        DeleteInputCommand = new RelayCommand(async () => await DeleteSelectedInputAsync(), () => IoMonitor.SelectedInput is not null && CanEditIoConfiguration());
        DeleteOutputCommand = new RelayCommand(async () => await DeleteSelectedOutputAsync(), () => IoMonitor.SelectedOutput is not null && CanEditIoConfiguration());
        SaveIoConfigCommand = new RelayCommand(async () => await SaveIoConfigAsync(), CanEditIoConfiguration);
        LoadIoConfigCommand = new RelayCommand(async () => await LoadIoConfigAsync(), CanEditIoConfiguration);
        AddCylinderCommand = new RelayCommand(async () => await AddCylinderAsync(), CanEditIoConfiguration);
        DeleteCylinderCommand = new RelayCommand(async () => await DeleteSelectedCylinderAsync(), () => CylinderMonitor.SelectedCylinder is not null && CanEditIoConfiguration());
        SaveCylinderConfigCommand = new RelayCommand(async () => await SaveCylinderConfigAsync(), CanEditIoConfiguration);
        LoadCylinderConfigCommand = new RelayCommand(async () => await LoadCylinderConfigAsync(), CanEditIoConfiguration);
        AddMagazineCommand = new RelayCommand(async () => await AddMagazineAsync(), CanEditIoConfiguration);
        DeleteMagazineCommand = new RelayCommand(async () => await DeleteSelectedMagazineAsync(), () => MagazineMonitor.SelectedMagazine is not null && CanEditIoConfiguration());
        AddMagazinePositionCommand = new RelayCommand(AddMagazinePosition, () => MagazineMonitor.SelectedMagazine is not null);
        DeleteMagazinePositionCommand = new RelayCommand(DeleteSelectedMagazinePosition, () => MagazineMonitor.SelectedMagazine?.CanDeleteSelectedPosition == true);
        SaveMagazineConfigCommand = new RelayCommand(async () => await SaveMagazineConfigAsync(), CanEditIoConfiguration);
        LoadMagazineConfigCommand = new RelayCommand(async () => await LoadMagazineConfigAsync(), CanEditIoConfiguration);
        TeachMagazinePositionCommand = new RelayCommand(TeachSelectedMagazinePosition, () => MagazineMonitor.SelectedMagazine?.SelectedPosition is not null && HasConfiguredAxesForMagazine());
        MoveMagazinePositionCommand = new RelayCommand(async () => await MoveSelectedMagazinePositionAsync(), () => MagazineMonitor.SelectedMagazine?.SelectedPosition is not null && HasConfiguredAxesForMagazine());
        ScanMagazineCommand = new RelayCommand(async () => await ScanMagazineAsync(), CanScanMagazine);
        AddWorkHeadCommand = new RelayCommand(async () => await AddWorkHeadAsync(), CanEditIoConfiguration);
        DeleteWorkHeadCommand = new RelayCommand(async () => await DeleteSelectedWorkHeadAsync(), () => WorkHeadMonitor.SelectedWorkHead is not null && CanEditIoConfiguration());
        SaveWorkHeadConfigCommand = new RelayCommand(async () => await SaveWorkHeadConfigAsync(), CanEditIoConfiguration);
        LoadWorkHeadConfigCommand = new RelayCommand(async () => await LoadWorkHeadConfigAsync(), CanEditIoConfiguration);
        MoveWorkHeadCommand = new RelayCommand(async () => await MoveSelectedWorkHeadAsync(), () => !string.IsNullOrWhiteSpace(SelectedWorkHeadMotionName));
        TeachWorkHeadCommand = new RelayCommand(TeachSelectedWorkHeadPosition, () => !string.IsNullOrWhiteSpace(SelectedWorkHeadMotionName));
        AddWorkHeadPositionCommand = new RelayCommand(AddWorkHeadPosition, () => WorkHeadMonitor.SelectedWorkHead is not null);
        DeleteWorkHeadPositionCommand = new RelayCommand(DeleteWorkHeadPosition, () => WorkHeadMonitor.SelectedWorkHead is not null);
        TeachToWorkHeadPositionCommand = new RelayCommand(TeachToSelectedWorkHeadPosition, () => !string.IsNullOrWhiteSpace(SelectedWorkHeadMotionName) && !string.IsNullOrWhiteSpace(SelectedWorkHeadPositionName));
        MoveToWorkHeadPositionCommand = new RelayCommand(async () => await MoveToSelectedWorkHeadPositionAsync(), () => !string.IsNullOrWhiteSpace(SelectedWorkHeadMotionName) && !string.IsNullOrWhiteSpace(SelectedWorkHeadPositionName));
        AddPositionSetupCommand = new RelayCommand(async () => await AddPositionSetupAsync(), CanEditIoConfiguration);
        DeletePositionSetupCommand = new RelayCommand(async () => await DeleteSelectedPositionSetupAsync(), () => PositionSetupMonitor.SelectedItem is not null && CanEditIoConfiguration());
        SavePositionSetupConfigCommand = new RelayCommand(async () => await SavePositionSetupConfigAsync(), CanEditIoConfiguration);
        LoadPositionSetupConfigCommand = new RelayCommand(async () => await LoadPositionSetupConfigAsync(), CanEditIoConfiguration);
        AddPositionSetupPositionCommand = new RelayCommand(AddPositionSetupPosition, () => PositionSetupMonitor.SelectedItem is not null);
        DeletePositionSetupPositionCommand = new RelayCommand(DeleteSelectedPositionSetupPosition, () => PositionSetupMonitor.SelectedItem?.HasSelectedPosition == true);
        TeachPositionSetupCommand = new RelayCommand(TeachSelectedPositionSetup, () => PositionSetupMonitor.SelectedItem?.HasSelectedPosition == true && PositionSetupMonitor.SelectedItem?.HasAnyConfiguredAxis == true);
        MovePositionSetupCommand = new RelayCommand(async () => await MoveSelectedPositionSetupAsync(), () => PositionSetupMonitor.SelectedItem?.HasSelectedPosition == true && PositionSetupMonitor.SelectedItem?.HasAnyConfiguredAxis == true);
        _axisConsoleCoordinator = new AxisConsoleCoordinator(AxisMonitor, AxisDebug, AxisParameterEditor);
        _ioMonitorCoordinator = new IoMonitorCoordinator(IoMonitor, (RelayCommand)DeleteInputCommand, (RelayCommand)DeleteOutputCommand);
        _ioMonitorCoordinator.Initialize();
        _clockTimer = new Timer(_ => CurrentBeijingTime = GetBeijingTimeString(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    public DashboardViewModel Dashboard { get; }

    public NavigationPage SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (_selectedPage == value)
            {
                return;
            }

            _selectedPage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentPageTitle));
            OnPropertyChanged(nameof(CurrentPageDescription));
        }
    }

    public string CurrentPageTitle => SelectedPage switch
    {
        NavigationPage.Dashboard => "Dashboard",
        NavigationPage.EtherCat => "EtherCAT Monitor",
        NavigationPage.Axis => "Axis Console",
        NavigationPage.Io => "IO Monitor",
        NavigationPage.Cylinder => "Cylinder Monitor",
        NavigationPage.Magazine => "Magazine Monitor",
        NavigationPage.WorkHead => "Work Head",
        NavigationPage.PositionSetup => "Position Setup",
        NavigationPage.Alarm => "Alarm Center",
        NavigationPage.EventLog => "Event Log",
        _ => "Dashboard"
    };

    public string CurrentPageDescription => SelectedPage switch
    {
        NavigationPage.Dashboard => "System overview, network health and recent events.",
        NavigationPage.EtherCat => "Realtime EtherCAT slave status, topology and fault visibility.",
        NavigationPage.Axis => "Axis control, jog operation and controller parameters.",
        NavigationPage.Io => "Digital input and output configuration with runtime status.",
        NavigationPage.Cylinder => "Cylinder configuration, runtime state and event tracking.",
        NavigationPage.Magazine => "Magazine configuration, runtime state and layer parameter setup.",
        NavigationPage.WorkHead => "Work head objects, position management and vacuum IO setup.",
        NavigationPage.PositionSetup => "Reusable position objects, axis mapping and target values.",
        NavigationPage.EventLog => "SQLite event history — filter by time, module, axis, level and object.",
        NavigationPage.Alarm => "Active alarms, severity overview and alarm investigation.",
        _ => "System overview, network health and recent events."
    };

    public string CurrentBeijingTime
    {
        get => _currentBeijingTime;
        private set
        {
            if (_currentBeijingTime == value)
            {
                return;
            }

            _currentBeijingTime = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> WorkHeadNames => WorkHeadMonitor.WorkHeads.Select(item => item.Name).OrderBy(item => item).ToList();

    public string? SelectedWorkHeadMotionName { get => _selectedWorkHeadMotionName; set { if (_selectedWorkHeadMotionName == value) return; _selectedWorkHeadMotionName = value; OnPropertyChanged(); (MoveWorkHeadCommand as RelayCommand)?.RaiseCanExecuteChanged(); (TeachWorkHeadCommand as RelayCommand)?.RaiseCanExecuteChanged(); (AddWorkHeadPositionCommand as RelayCommand)?.RaiseCanExecuteChanged(); (DeleteWorkHeadPositionCommand as RelayCommand)?.RaiseCanExecuteChanged(); (TeachToWorkHeadPositionCommand as RelayCommand)?.RaiseCanExecuteChanged(); (MoveToWorkHeadPositionCommand as RelayCommand)?.RaiseCanExecuteChanged(); OnPropertyChanged(nameof(WorkHeadPositionNames)); SelectedWorkHeadPositionName = null; } }
    public double WorkHeadTargetX { get => _workHeadTargetX; set { if (_workHeadTargetX == value) return; _workHeadTargetX = value; OnPropertyChanged(); } }
    public double WorkHeadTargetY { get => _workHeadTargetY; set { if (_workHeadTargetY == value) return; _workHeadTargetY = value; OnPropertyChanged(); } }
    public double WorkHeadTargetZ { get => _workHeadTargetZ; set { if (_workHeadTargetZ == value) return; _workHeadTargetZ = value; OnPropertyChanged(); } }
    public double WorkHeadTargetR { get => _workHeadTargetR; set { if (_workHeadTargetR == value) return; _workHeadTargetR = value; OnPropertyChanged(); } }
    public double WorkHeadMoveVelocity { get => _workHeadMoveVelocity; set { if (_workHeadMoveVelocity == value) return; _workHeadMoveVelocity = value; OnPropertyChanged(); } }
    public double WorkHeadMoveAcceleration { get => _workHeadMoveAcceleration; set { if (_workHeadMoveAcceleration == value) return; _workHeadMoveAcceleration = value; OnPropertyChanged(); } }
    public double WorkHeadMoveDeceleration { get => _workHeadMoveDeceleration; set { if (_workHeadMoveDeceleration == value) return; _workHeadMoveDeceleration = value; OnPropertyChanged(); } }

    public string? SelectedWorkHeadPositionName { get => _selectedWorkHeadPositionName; set { if (_selectedWorkHeadPositionName == value) return; _selectedWorkHeadPositionName = value; OnPropertyChanged(); (DeleteWorkHeadPositionCommand as RelayCommand)?.RaiseCanExecuteChanged(); (TeachToWorkHeadPositionCommand as RelayCommand)?.RaiseCanExecuteChanged(); (MoveToWorkHeadPositionCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

    public IReadOnlyList<WorkHeadPosition> GetWorkHeadPositions(string workHeadName)
    {
        var workHead = WorkHeadMonitor.WorkHeads.FirstOrDefault(w => string.Equals(w.Name, workHeadName, StringComparison.OrdinalIgnoreCase));
        return workHead?.Positions?.ToList() ?? new List<WorkHeadPosition>();
    }
    public string OperationStatus
    {
        get => _operationStatus;
        private set
        {
            if (_operationStatus == value)
            {
                return;
            }

            _operationStatus = value;
            OnPropertyChanged();
        }
    }

    public EtherCatMonitorViewModel EtherCatMonitor { get; }
    public AxisMonitorViewModel AxisMonitor { get; }
    public AxisDebugViewModel AxisDebug { get; }
    public IoMonitorViewModel IoMonitor { get; }
    public IoEventLogViewModel IoEventLog { get; }
    public CylinderMonitorViewModel CylinderMonitor { get; }
    public MagazineMonitorViewModel MagazineMonitor { get; }
    public WorkHeadMonitorViewModel WorkHeadMonitor { get; }
    public PositionSetupMonitorViewModel PositionSetupMonitor { get; }
    public CylinderEventLogViewModel CylinderEventLog { get; }

    public RuntimeEventLogQueryViewModel EventLogQuery { get; }
    public MagazineEventLogViewModel MagazineEventLog { get; }
    public WorkHeadEventLogViewModel WorkHeadEventLog { get; }
    public PositionSetupEventLogViewModel PositionSetupEventLog { get; }
    public AxisParameterEditorViewModel AxisParameterEditor { get; }
    public AxisDataCaptureViewModel AxisDataCapture { get; }
    public AlarmViewModel Alarm { get; }

    public ICommand EmergencyStopCommand { get; }
    public ICommand ClearEmergencyStopCommand { get; }
    public ICommand ReconnectCommand { get; }
    public ICommand AddAxisCommand { get; }
    public ICommand DeleteAxisCommand { get; }
    public ICommand SaveAxisConsoleConfigCommand { get; }
    public ICommand LoadAxisConsoleConfigCommand { get; }
    public ICommand AddInputCommand { get; }
    public ICommand AddOutputCommand { get; }
    public ICommand DeleteInputCommand { get; }
    public ICommand DeleteOutputCommand { get; }
    public ICommand SaveIoConfigCommand { get; }
    public ICommand LoadIoConfigCommand { get; }
    public ICommand AddCylinderCommand { get; }
    public ICommand DeleteCylinderCommand { get; }
    public ICommand SaveCylinderConfigCommand { get; }
    public ICommand LoadCylinderConfigCommand { get; }
    public ICommand AddMagazineCommand { get; }
    public ICommand DeleteMagazineCommand { get; }
    public ICommand AddMagazinePositionCommand { get; }
    public ICommand DeleteMagazinePositionCommand { get; }
    public ICommand SaveMagazineConfigCommand { get; }
    public ICommand LoadMagazineConfigCommand { get; }
    public ICommand TeachMagazinePositionCommand { get; }
    public ICommand MoveMagazinePositionCommand { get; }
    public ICommand ScanMagazineCommand { get; }
    public ICommand AddWorkHeadCommand { get; }
    public ICommand DeleteWorkHeadCommand { get; }
    public ICommand SaveWorkHeadConfigCommand { get; }
    public ICommand LoadWorkHeadConfigCommand { get; }
    public ICommand MoveWorkHeadCommand { get; }
    public ICommand TeachWorkHeadCommand { get; }
    public ICommand AddWorkHeadPositionCommand { get; }
    public ICommand DeleteWorkHeadPositionCommand { get; }
    public ICommand TeachToWorkHeadPositionCommand { get; }
    public ICommand MoveToWorkHeadPositionCommand { get; }
    public ICommand AddPositionSetupCommand { get; }
    public ICommand DeletePositionSetupCommand { get; }
    public ICommand SavePositionSetupConfigCommand { get; }
    public ICommand LoadPositionSetupConfigCommand { get; }
    public ICommand TeachPositionSetupCommand { get; }
    public ICommand MovePositionSetupCommand { get; }
    public ICommand AddPositionSetupPositionCommand { get; }
    public ICommand DeletePositionSetupPositionCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _systemAppService.InitializeAsync(cancellationToken);
        await _axisConsoleCoordinator.InitializeAsync(cancellationToken);
        // 首次启动自动加载位置设定（不弹确认）
        await LoadPositionSetupConfigOnStartupAsync(cancellationToken);
        RefreshViewModels(force: true);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _systemAppService.RefreshAsync(cancellationToken);
        RefreshViewModels(force: true);
    }

    public void RefreshViewModels()
    {
        RefreshViewModels(force: false);
    }

    public void RefreshViewModels(bool force)
    {
        AxisDebug.RefreshCommandStates();
        AxisParameterEditor.RefreshCommandStates();
        AxisDataCapture.RefreshCommandStates();
        RaiseAxisDeleteCanExecuteChanged();
        (AddInputCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddOutputCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteInputCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteOutputCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveIoConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LoadIoConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddCylinderCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCylinderCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveCylinderConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LoadCylinderConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddMagazineCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteMagazineCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveMagazineConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LoadMagazineConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (TeachMagazinePositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MoveMagazinePositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ScanMagazineCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddWorkHeadCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteWorkHeadCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveWorkHeadConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LoadWorkHeadConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
        var now = DateTime.UtcNow;

        if (force || now - _lastDashboardRefreshUtc >= TimeSpan.FromMilliseconds(500))
        {
            Dashboard.Refresh(_controllerRuntimeState.LastControllerStatus);
            _lastDashboardRefreshUtc = now;
        }

        if (force || now - _lastAxisRefreshUtc >= TimeSpan.FromMilliseconds(300))
        {
            AxisMonitor.RefreshAll();
            _lastAxisRefreshUtc = now;
        }

        if (force || now - _lastAlarmRefreshUtc >= TimeSpan.FromMilliseconds(1000))
        {
            Alarm.Refresh();
            _lastAlarmRefreshUtc = now;
        }

        if (force || now - _lastIoRefreshUtc >= TimeSpan.FromMilliseconds(300))
        {
            IoMonitor.RefreshAll();
            _lastIoRefreshUtc = now;
        }

        if (force || now - _lastIoEventRefreshUtc >= TimeSpan.FromMilliseconds(500))
        {
            IoEventLog.Refresh();
            _lastIoEventRefreshUtc = now;
        }

        if (force || now - _lastCylinderRefreshUtc >= TimeSpan.FromMilliseconds(300))
        {
            RefreshCylinderStates();
            CylinderMonitor.RefreshAll();
            CylinderEventLog.Refresh();
            _lastCylinderRefreshUtc = now;
        }

        if (force || now - _lastMagazineRefreshUtc >= TimeSpan.FromMilliseconds(300))
        {
            MagazineMonitor.RefreshAll();
            MagazineEventLog.Refresh();
            _lastMagazineRefreshUtc = now;
        }

        if (force || now - _lastWorkHeadRefreshUtc >= TimeSpan.FromMilliseconds(300))
        {
            WorkHeadMonitor.RefreshAll();
            WorkHeadEventLog.Refresh();
            _lastWorkHeadRefreshUtc = now;
        }

        if (force || now - _lastPositionSetupRefreshUtc >= TimeSpan.FromMilliseconds(300))
        {
            PositionSetupEventLog.Refresh();
            (AddPositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeletePositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SavePositionSetupConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (LoadPositionSetupConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddPositionSetupPositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeletePositionSetupPositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TeachPositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MovePositionSetupCommand as RelayCommand)?.RaiseCanExecuteChanged();
            _lastPositionSetupRefreshUtc = now;
        }
    }

    private void RaiseAxisDeleteCanExecuteChanged()
    {
        (DeleteAxisCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private async Task AddAxisAsync()
    {
        var item = await _axisManagementAppService.CreateAxisForRuntimeAsync();
        var axis = _machine.Axes.FirstOrDefault(a => a.Id.Value == item.AxisNo);
        if (axis is null)
        {
            OperationStatus = $"Axis {item.AxisNo} 创建后未同步到运行时";
            return;
        }

        AxisMonitor.AddAxis(axis);
        RaiseAxisDeleteCanExecuteChanged();
        await _axisConsoleCoordinator.SyncSelectedAxisAsync(item.AxisNo);
        OperationStatus = $"Axis {item.AxisNo} 已新增（未保存）";
        RefreshViewModels(force: true);
    }

    private Task DeleteSelectedAxisAsync()
    {
        var selectedAxis = AxisMonitor.SelectedAxis;
        if (selectedAxis is null)
        {
            return Task.CompletedTask;
        }

        if (!UiGuards.Confirm(_dialogService, "删除 Axis", $"确定删除 Axis {selectedAxis.AxisNo} 吗？（未保存的删除）"))
        {
            OperationStatus = "已取消删除 Axis";
            return Task.CompletedTask;
        }

        var axisNo = selectedAxis.AxisNo;
        _machine.RemoveAxis(axisNo);
        AxisMonitor.RemoveAxis(axisNo);
        RaiseAxisDeleteCanExecuteChanged();
        OperationStatus = $"Axis {axisNo} 已删除（未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private async Task SaveAxisConsoleConfigAsync()
    {
        if (!UiGuards.Confirm(_dialogService, "保存 Axis 配置", "确定保存当前 Axis 列表和参数到配置文件吗？"))
        {
            OperationStatus = "已取消保存 Axis 配置";
            return;
        }

        // 先将参数编辑器的值同步到运行时，再构建完整列表
        if (AxisMonitor.SelectedAxis is not null && AxisParameterEditor.AxisNo >= 0)
        {
            var editorItem = new AxisMappingItem
            {
                AxisNo = AxisParameterEditor.AxisNo,
                Name = AxisParameterEditor.Name,
                Group = AxisParameterEditor.Group,
                IsMaster = AxisParameterEditor.IsMaster,
                MasterAxisName = string.IsNullOrWhiteSpace(AxisParameterEditor.MasterAxisName) ? null : AxisParameterEditor.MasterAxisName,
                SoftLimitPositive = AxisParameterEditor.SoftLimitPositive,
                SoftLimitNegative = AxisParameterEditor.SoftLimitNegative,
                WorkVelocity = AxisParameterEditor.WorkVelocity,
                SetupVelocity = AxisParameterEditor.SetupVelocity,
                Acceleration = AxisParameterEditor.Acceleration,
                Deceleration = AxisParameterEditor.Deceleration,
                PulseEquivalent = AxisParameterEditor.PulseEquivalent,
                HomeMode = AxisParameterEditor.HomeMode,
                ServoBinding = AxisParameterEditor.ServoBinding
            };
            await _axisManagementAppService.SyncAxisToRuntimeAsync(editorItem);
        }

        var items = new List<AxisMappingItem>();
        foreach (var ax in _machine.Axes)
        {
            items.Add(new AxisMappingItem
            {
                AxisNo = ax.Id.Value,
                Name = ax.Name,
                Group = string.Empty,
                IsMaster = false,
                SoftLimitPositive = ax.SoftLimit?.Maximum,
                SoftLimitNegative = ax.SoftLimit?.Minimum,
                WorkVelocity = ax.WorkVelocity,
                SetupVelocity = ax.SetupVelocity,
                Acceleration = ax.Acceleration,
                Deceleration = ax.Deceleration,
                PulseEquivalent = ax.PulseEquivalent,
                HomeMode = ax.HomeMode,
                ServoBinding = ax.ServoBinding
            });
        }

        await _axisManagementAppService.SaveAllAxesAsync(items);

        // 同时写入控制器参数
        if (AxisParameterEditor.AxisNo >= 0)
        {
            await AxisParameterEditor.WriteControllerAsync();
        }

        OperationStatus = $"Axis 配置已保存并写入控制器，共 {items.Count} 个轴";
        if (AxisMonitor.SelectedAxis is not null)
        {
            await AxisParameterEditor.SyncAxisNoAsync(AxisMonitor.SelectedAxis.AxisNo);
        }
        RefreshViewModels(force: true);
    }

    private async Task LoadAxisConsoleConfigAsync()
    {
        if (!UiGuards.Confirm(_dialogService, "加载 Axis 配置", "确定从配置文件重新加载 Axis 配置吗？未保存修改将丢失。"))
        {
            OperationStatus = "已取消加载 Axis 配置";
            return;
        }

        var allAxes = await _axisManagementAppService.LoadAllAxesAsync();

        // Remove axes from runtime that are not in config
        var configAxisNos = allAxes.Select(a => a.AxisNo).ToHashSet();
        foreach (var runtimeAxis in _machine.Axes.ToList())
        {
            if (!configAxisNos.Contains(runtimeAxis.Id.Value))
            {
                _machine.RemoveAxis(runtimeAxis.Id.Value);
            }
        }

        // Sync config axes to runtime (creates missing ones, updates existing)
        foreach (var configAxis in allAxes)
        {
            await _axisManagementAppService.SyncAxisToRuntimeAsync(configAxis);
        }

        AxisMonitor.ReloadFromMachine();

        if (AxisMonitor.SelectedAxis is not null)
        {
            await AxisParameterEditor.SyncAxisNoAsync(AxisMonitor.SelectedAxis.AxisNo);
        }

        RaiseAxisDeleteCanExecuteChanged();
        OperationStatus = "Axis 配置已重新加载";
        RefreshViewModels(force: true);
    }

    private Task AddIoPointAsync(bool isOutput)
    {
        var existingAddresses = IoMonitor.GetAllIoPoints()
            .Where(io => io.IsOutput == isOutput)
            .Select(io => io.Address)
            .OrderBy(address => address)
            .ToList();

        var nextAddress = 0;
        while (existingAddresses.Contains(nextAddress))
        {
            nextAddress++;
        }

        var ioPoint = new IoPoint(isOutput ? $"DO_{nextAddress}" : $"DI_{nextAddress}", nextAddress, isOutput);
        IoMonitor.AddIoPoint(ioPoint);
        OperationStatus = $"{(isOutput ? "DO" : "DI")} {nextAddress} 已新增（未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private Task DeleteSelectedInputAsync()
    {
        var selected = IoMonitor.SelectedInput;
        if (selected is null)
        {
            return Task.CompletedTask;
        }

        if (!UiGuards.Confirm(_dialogService, "删除输入点", $"确定删除 DI {selected.Address} 吗？删除后需点击 Save 才会写入配置。"))
        {
            OperationStatus = "已取消删除 DI";
            return Task.CompletedTask;
        }

        IoMonitor.RemoveIoPoint(false, selected.Address);
        OperationStatus = $"DI {selected.Address} 已删除（未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private Task DeleteSelectedOutputAsync()
    {
        var selected = IoMonitor.SelectedOutput;
        if (selected is null)
        {
            return Task.CompletedTask;
        }

        if (!UiGuards.Confirm(_dialogService, "删除输出点", $"确定删除 DO {selected.Address} 吗？删除后需点击 Save 才会写入配置。"))
        {
            OperationStatus = "已取消删除 DO";
            return Task.CompletedTask;
        }

        IoMonitor.RemoveIoPoint(true, selected.Address);
        OperationStatus = $"DO {selected.Address} 已删除（未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private Task AddCylinderAsync()
    {
        var index = CylinderMonitor.Cylinders.Count + 1;
        var names = CylinderMonitor.Cylinders.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = $"Cylinder {index}";
        while (names.Contains(name))
        {
            index++;
            name = $"Cylinder {index}";
        }

        var cylinder = new Cylinder(name, -1, -1, -1, -1, string.Empty, 3000);
        CylinderMonitor.AddCylinder(cylinder);
        OperationStatus = $"Cylinder {name} 已新增（未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private Task DeleteSelectedCylinderAsync()
    {
        var selected = CylinderMonitor.SelectedCylinder;
        if (selected is null)
        {
            return Task.CompletedTask;
        }

        if (!UiGuards.Confirm(_dialogService, "删除 Cylinder", $"确定删除 Cylinder {selected.Name} 吗？删除后需点击 Save 才会写入配置。"))
        {
            OperationStatus = "已取消删除 Cylinder";
            return Task.CompletedTask;
        }

        CylinderMonitor.RemoveCylinder(selected.Name);
        OperationStatus = $"Cylinder {selected.Name} 已删除（未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private void TeachSelectedWorkHeadPosition()
    {
        if (string.IsNullOrWhiteSpace(SelectedWorkHeadMotionName))
        {
            OperationStatus = "请先选择 WorkHead";
            return;
        }

        var workHead = WorkHeadMonitor.WorkHeads.FirstOrDefault(item => string.Equals(item.Name, SelectedWorkHeadMotionName, StringComparison.OrdinalIgnoreCase));
        if (workHead is null)
        {
            OperationStatus = $"未找到 WorkHead: {SelectedWorkHeadMotionName}";
            return;
        }

        if (workHead.XAxisNo >= 0)
        {
            var axis = _machine.Axes.FirstOrDefault(item => item.Id.Value == workHead.XAxisNo);
            if (axis is not null) WorkHeadTargetX = ToEngineeringPosition(axis);
        }
        if (workHead.YAxisNo >= 0)
        {
            var axis = _machine.Axes.FirstOrDefault(item => item.Id.Value == workHead.YAxisNo);
            if (axis is not null) WorkHeadTargetY = ToEngineeringPosition(axis);
        }
        if (workHead.ZAxisNo >= 0)
        {
            var axis = _machine.Axes.FirstOrDefault(item => item.Id.Value == workHead.ZAxisNo);
            if (axis is not null) WorkHeadTargetZ = ToEngineeringPosition(axis);
        }
        if (workHead.RAxisNo >= 0)
        {
            var axis = _machine.Axes.FirstOrDefault(item => item.Id.Value == workHead.RAxisNo);
            if (axis is not null) WorkHeadTargetR = ToEngineeringPosition(axis);
        }

        OperationStatus = $"WorkHead {workHead.Name} 当前坐标已 Teach";
    }

    private void AddWorkHeadPosition()
    {
        if (WorkHeadMonitor.SelectedWorkHead is null) return;
        WorkHeadMonitor.SelectedWorkHead.AddPosition();
        OperationStatus = $"{WorkHeadMonitor.SelectedWorkHead.Name} 已添加 Position";
    }

    private void DeleteWorkHeadPosition()
    {
        if (WorkHeadMonitor.SelectedWorkHead is null) return;
        var selectedWorkHead = WorkHeadMonitor.SelectedWorkHead;
        var selectedPositionName = selectedWorkHead.PositionSelector;
        if (string.IsNullOrWhiteSpace(selectedPositionName))
        {
            selectedPositionName = selectedWorkHead.Positions.FirstOrDefault()?.Name;
        }
        if (string.IsNullOrWhiteSpace(selectedPositionName)) return;

        if (!UiGuards.Confirm(_dialogService, "删除 WorkHead Position", $"确定删除 {selectedWorkHead.Name} 的 Position {selectedPositionName} 吗？删除后需点击 Save 才会写入配置。"))
        {
            OperationStatus = "已取消删除 WorkHead Position";
            return;
        }

        selectedWorkHead.DeleteSelectedPosition();
        OperationStatus = $"{selectedWorkHead.Name} 已删除 Position（未保存）";
    }

    private void TeachToSelectedWorkHeadPosition()
    {
        if (string.IsNullOrWhiteSpace(SelectedWorkHeadMotionName) || string.IsNullOrWhiteSpace(SelectedWorkHeadPositionName)) return;
        var workHead = WorkHeadMonitor.WorkHeads.FirstOrDefault(w => string.Equals(w.Name, SelectedWorkHeadMotionName, StringComparison.OrdinalIgnoreCase));
        if (workHead is null) return;

        var pos = workHead.Positions.FirstOrDefault(p => p.Name == SelectedWorkHeadPositionName);
        if (pos is null) return;

        if (workHead.XAxisNo >= 0)
        {
            var axis = _machine.Axes.FirstOrDefault(item => item.Id.Value == workHead.XAxisNo);
            if (axis is not null) pos.X = ToEngineeringPosition(axis);
        }
        if (workHead.YAxisNo >= 0)
        {
            var axis = _machine.Axes.FirstOrDefault(item => item.Id.Value == workHead.YAxisNo);
            if (axis is not null) pos.Y = ToEngineeringPosition(axis);
        }
        if (workHead.ZAxisNo >= 0)
        {
            var axis = _machine.Axes.FirstOrDefault(item => item.Id.Value == workHead.ZAxisNo);
            if (axis is not null) pos.Z = ToEngineeringPosition(axis);
        }
        if (workHead.RAxisNo >= 0)
        {
            var axis = _machine.Axes.FirstOrDefault(item => item.Id.Value == workHead.RAxisNo);
            if (axis is not null) pos.R = ToEngineeringPosition(axis);
        }

        OnPropertyChanged(nameof(WorkHeadPositionNames));
        OperationStatus = $"位置 {pos.Name} 坐标已更新";
    }

    private async Task MoveToSelectedWorkHeadPositionAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedWorkHeadMotionName) || string.IsNullOrWhiteSpace(SelectedWorkHeadPositionName)) return;
        var workHead = WorkHeadMonitor.WorkHeads.FirstOrDefault(w => string.Equals(w.Name, SelectedWorkHeadMotionName, StringComparison.OrdinalIgnoreCase));
        if (workHead is null) return;

        var pos = workHead.Positions.FirstOrDefault(p => p.Name == SelectedWorkHeadPositionName);
        if (pos is null) return;

        var configuredAxes = new[] { (no: workHead.XAxisNo, name: "X"), (no: workHead.YAxisNo, name: "Y"), (no: workHead.ZAxisNo, name: "Z"), (no: workHead.RAxisNo, name: "R") }.Where(a => a.no >= 0).ToList();
        var missingAxes = configuredAxes.Where(a => _machine.Axes.All(ax => ax.Id.Value != a.no)).Select(a => a.no).ToList();
        if (missingAxes.Count > 0)
        {
            OperationStatus = $"WorkHead {workHead.Name} 存在未配置到运行时的轴: {string.Join(", ", missingAxes)}";
            return;
        }

        // Temporarily set targets and call the existing move logic
        var savedX = WorkHeadTargetX;
        var savedY = WorkHeadTargetY;
        var savedZ = WorkHeadTargetZ;
        var savedR = WorkHeadTargetR;

        WorkHeadTargetX = pos.X;
        WorkHeadTargetY = pos.Y;
        WorkHeadTargetZ = pos.Z;
        WorkHeadTargetR = pos.R;

        await MoveSelectedWorkHeadAsync();

        WorkHeadTargetX = savedX;
        WorkHeadTargetY = savedY;
        WorkHeadTargetZ = savedZ;
        WorkHeadTargetR = savedR;
    }

    public IReadOnlyList<string> WorkHeadPositionNames
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SelectedWorkHeadMotionName)) return Array.Empty<string>();
            var workHead = WorkHeadMonitor.WorkHeads.FirstOrDefault(w => string.Equals(w.Name, SelectedWorkHeadMotionName, StringComparison.OrdinalIgnoreCase));
            return workHead?.Positions.Select(p => p.Name).ToList() ?? new List<string>();
        }
    }

    private async Task MoveSelectedWorkHeadAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedWorkHeadMotionName))
        {
            OperationStatus = "请先选择 WorkHead";
            return;
        }

        var workHead = WorkHeadMonitor.WorkHeads.FirstOrDefault(item => string.Equals(item.Name, SelectedWorkHeadMotionName, StringComparison.OrdinalIgnoreCase));
        if (workHead is null)
        {
            OperationStatus = $"未找到 WorkHead: {SelectedWorkHeadMotionName}";
            return;
        }

        var eventName = $"WorkHead {workHead.Name}";
        var configuredAxes = new[] { (no: workHead.XAxisNo, name: "X"), (no: workHead.YAxisNo, name: "Y"), (no: workHead.ZAxisNo, name: "Z"), (no: workHead.RAxisNo, name: "R") }.Where(a => a.no >= 0).ToList();
        var missingAxes = configuredAxes.Where(a => _machine.Axes.All(ax => ax.Id.Value != a.no)).Select(a => a.no).ToList();
        if (missingAxes.Count > 0)
        {
            var message = $"WorkHead {workHead.Name} 存在未配置到运行时的轴: {string.Join(", ", missingAxes)}";
            OperationStatus = message;
            _commandFeedbackRuntimeState.AddFailed("WorkHeadMove", message: message);
            return;
        }

        var hasAnyAxis = workHead.XAxisNo >= 0 || workHead.YAxisNo >= 0 || workHead.ZAxisNo >= 0 || workHead.RAxisNo >= 0;
        if (!hasAnyAxis)
        {
            var message = $"WorkHead {workHead.Name} 没有可运动的轴";
            OperationStatus = message;
            _commandFeedbackRuntimeState.AddFailed("WorkHeadMove", message: message);
            return;
        }

        try
        {
            _commandFeedbackRuntimeState.AddStarted("WorkHeadMove", message: $"{eventName} started");

            if (workHead.ZAxisNo >= 0)
            {
                var safeZResult = await _motionAppService.MoveAbsoluteAsync(new MotionControl.Application.DTOs.MoveAxisCommandDto(
                    workHead.ZAxisNo,
                    workHead.SafeZ,
                    WorkHeadMoveVelocity,
                    WorkHeadMoveAcceleration,
                    WorkHeadMoveDeceleration));
                if (!safeZResult.Success)
                {
                    var message = $"{workHead.Name} Z轴抬升失败: {safeZResult.ErrorMessage}";
                    OperationStatus = message;
                    _commandFeedbackRuntimeState.AddFailed("WorkHeadMove", message: message);
                    return;
                }
            }

            // 多轴运动显式串行执行。底层 ZmcAxisNativeFacade._multiStepLock
            // 会串行化所有复合运动命令（SPEED→ACCEL→DECEL→Move），
            // 上层显式 await 与底层行为一致，避免误导后续维护者。
            if (workHead.XAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(new MotionControl.Application.DTOs.MoveAxisCommandDto(
                    workHead.XAxisNo, WorkHeadTargetX, WorkHeadMoveVelocity, WorkHeadMoveAcceleration, WorkHeadMoveDeceleration));
                if (!r.Success)
                {
                    var message = $"{workHead.Name} X轴运动失败: {r.ErrorMessage}";
                    OperationStatus = message;
                    _commandFeedbackRuntimeState.AddFailed("WorkHeadMove", message: message);
                    return;
                }
            }
            if (workHead.YAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(new MotionControl.Application.DTOs.MoveAxisCommandDto(
                    workHead.YAxisNo, WorkHeadTargetY, WorkHeadMoveVelocity, WorkHeadMoveAcceleration, WorkHeadMoveDeceleration));
                if (!r.Success)
                {
                    var message = $"{workHead.Name} Y轴运动失败: {r.ErrorMessage}";
                    OperationStatus = message;
                    _commandFeedbackRuntimeState.AddFailed("WorkHeadMove", message: message);
                    return;
                }
            }
            if (workHead.RAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(new MotionControl.Application.DTOs.MoveAxisCommandDto(
                    workHead.RAxisNo, WorkHeadTargetR, WorkHeadMoveVelocity, WorkHeadMoveAcceleration, WorkHeadMoveDeceleration));
                if (!r.Success)
                {
                    var message = $"{workHead.Name} R轴运动失败: {r.ErrorMessage}";
                    OperationStatus = message;
                    _commandFeedbackRuntimeState.AddFailed("WorkHeadMove", message: message);
                    return;
                }
            }

            if (workHead.ZAxisNo >= 0)
            {
                var targetZResult = await _motionAppService.MoveAbsoluteAsync(new MotionControl.Application.DTOs.MoveAxisCommandDto(
                    workHead.ZAxisNo,
                    WorkHeadTargetZ,
                    WorkHeadMoveVelocity,
                    WorkHeadMoveAcceleration,
                    WorkHeadMoveDeceleration));
                if (!targetZResult.Success)
                {
                    var message = $"{workHead.Name} Z轴定位失败: {targetZResult.ErrorMessage}";
                    OperationStatus = message;
                    _commandFeedbackRuntimeState.AddFailed("WorkHeadMove", message: message);
                    return;
                }
            }

            OperationStatus = $"WorkHead {workHead.Name} 已执行定位";
            _commandFeedbackRuntimeState.AddSucceeded("WorkHeadMove", message: $"{eventName} completed");
        }
        catch (Exception ex)
        {
            var message = $"{eventName} failed: {ex.Message}";
            OperationStatus = message;
            _commandFeedbackRuntimeState.AddFailed("WorkHeadMove", message: message);
        }
    }

    private Task AddWorkHeadAsync()
    {
        var index = WorkHeadMonitor.WorkHeads.Count + 1;
        var names = WorkHeadMonitor.WorkHeads.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = $"WorkHead {index}";
        while (names.Contains(name))
        {
            index++;
            name = $"WorkHead {index}";
        }

        var workHead = new WorkHead(name, string.Empty, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 3000, new[] { new WorkHeadPosition("Position 1", string.Empty, 0, 0, 0, 0) }, 0);
        WorkHeadMonitor.AddWorkHead(workHead);
        OnPropertyChanged(nameof(WorkHeadNames));
        OperationStatus = $"WorkHead {name} 已新增（未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private Task DeleteSelectedWorkHeadAsync()
    {
        var selected = WorkHeadMonitor.SelectedWorkHead;
        if (selected is null) return Task.CompletedTask;
        if (!UiGuards.Confirm(_dialogService, "删除 WorkHead", $"确定删除 WorkHead {selected.Name} 吗？删除后需点击 Save 才会写入配置。"))
        {
            OperationStatus = "已取消删除 WorkHead";
            return Task.CompletedTask;
        }

        WorkHeadMonitor.RemoveWorkHead(selected.Name);
        OnPropertyChanged(nameof(WorkHeadNames));
        OperationStatus = $"WorkHead {selected.Name} 已删除（未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private async Task SaveIoConfigAsync()
    {
        var items = IoMonitor.GetAllIoPoints()
            .Select(io => new MotionControl.Infrastructure.Configuration.IoPointConfigItem
            {
                Name = io.Name,
                Address = io.Address,
                IsOutput = io.IsOutput,
                Description = io.Description
            })
            .ToList();

        var duplicateAddress = items
            .GroupBy(io => new { io.IsOutput, io.Address })
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateAddress is not null)
        {
            var msg = $"存在重复地址: {(duplicateAddress.Key.IsOutput ? "DO" : "DI")} {duplicateAddress.Key.Address}";
            OperationStatus = msg;
            _dialogService.ShowWarning(msg, "IO 配置校验失败");
            return;
        }

        var invalidItem = items.FirstOrDefault(io => string.IsNullOrWhiteSpace(io.Name) || io.Address < 0);
        if (invalidItem is not null)
        {
            var msg = "IO 配置存在空名称或非法地址，保存已取消";
            OperationStatus = msg;
            _dialogService.ShowWarning(msg, "IO 配置校验失败");
            return;
        }

        if (!UiGuards.Confirm(_dialogService, "保存 IO 配置", "确定覆盖当前 IO 配置到 appsettings.json 吗？"))
        {
            OperationStatus = "已取消保存 IO 配置";
            return;
        }

        try
        {
            await _ioManagementAppService.SaveIoPointsAsync(items);
            await _ioManagementAppService.LoadIoPointsAsync();
            _ioMonitorCoordinator.AfterLoadOrReload();
            OperationStatus = $"IO 配置已保存，共 {items.Count} 个点位";
            RefreshViewModels(force: true);
        }
        catch (InvalidOperationException ex)
        {
            OperationStatus = ex.Message;
            _dialogService.ShowError(ex.Message, "IO 配置保存失败");
            return;
        }
    }

    private async Task SaveCylinderConfigAsync()
    {
        var items = CylinderMonitor.Cylinders
            .Select(cylinder => new MotionControl.Infrastructure.Configuration.CylinderConfigItem
            {
                Name = cylinder.Name,
                Description = cylinder.Description,
                ExtendSensorInputAddress = cylinder.ExtendSensorInputAddress,
                RetractSensorInputAddress = cylinder.RetractSensorInputAddress,
                ExtendOutputAddress = cylinder.ExtendOutputAddress,
                RetractOutputAddress = cylinder.RetractOutputAddress,
                ActionTimeoutMs = cylinder.ActionTimeoutMs
            })
            .ToList();

        if (!UiGuards.Confirm(_dialogService, "保存 Cylinder 配置", "确定覆盖当前 Cylinder 配置到 appsettings.json 吗？"))
        {
            OperationStatus = "已取消保存 Cylinder 配置";
            return;
        }

        try
        {
            await _cylinderManagementAppService.SaveCylindersAsync(items);
            await _cylinderManagementAppService.LoadCylindersAsync();
            OperationStatus = $"Cylinder 配置已保存，共 {items.Count} 个气缸";
            RefreshViewModels(force: true);
        }
        catch (InvalidOperationException ex)
        {
            OperationStatus = ex.Message;
            _dialogService.ShowError(ex.Message, "Cylinder 配置保存失败");
        }
    }

    private async Task LoadCylinderConfigAsync()
    {
        if (!UiGuards.Confirm(_dialogService, "加载 Cylinder 配置", "确定从 appsettings.json 重新加载 Cylinder 配置吗？未保存修改将丢失。"))
        {
            OperationStatus = "已取消加载 Cylinder 配置";
            return;
        }

        await _cylinderManagementAppService.LoadCylindersAsync();
        CylinderMonitor.ReloadFromMachine();
        OperationStatus = "Cylinder 配置已重新加载";
        RefreshViewModels(force: true);
    }

    private Task AddMagazineAsync()
    {
        var index = MagazineMonitor.Magazines.Count + 1;
        var names = MagazineMonitor.Magazines.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = $"Magazine {index}";
        while (names.Contains(name))
        {
            index++;
            name = $"Magazine {index}";
        }

        var magazine = new Magazine(name, string.Empty, -1, -1, -1, -1, -1, -1, 1, 0, 0, 200);
        magazine.EnsureDefaultPositions();
        MagazineMonitor.AddMagazine(magazine);
        OperationStatus = $"Magazine {name} 已新增（未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private Task DeleteSelectedMagazineAsync()
    {
        var selected = MagazineMonitor.SelectedMagazine;
        if (selected is null) return Task.CompletedTask;
        if (!UiGuards.Confirm(_dialogService, "删除 Magazine", $"确定删除 Magazine {selected.Name} 吗？删除后需点击 Save 才会写入配置。"))
        {
            OperationStatus = "已取消删除 Magazine";
            return Task.CompletedTask;
        }

        MagazineMonitor.RemoveMagazine(selected.Name);
        OperationStatus = $"Magazine {selected.Name} 已删除（未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private void AddMagazinePosition()
    {
        var selected = MagazineMonitor.SelectedMagazine;
        if (selected is null) return;

        selected.AddPosition();
        OperationStatus = $"Magazine {selected.Name} 新增位置 {selected.SelectedPosition?.Name}（未保存）";
        (DeleteMagazinePositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        RefreshViewModels(force: true);
    }

    private void DeleteSelectedMagazinePosition()
    {
        var selected = MagazineMonitor.SelectedMagazine;
        if (selected?.SelectedPosition is null) return;

        var positionName = selected.SelectedPosition.Name;
        if (selected.SelectedPosition.IsSystemDefault)
        {
            OperationStatus = $"Magazine {selected.Name} 的系统默认位 {positionName} 不允许删除";
            _dialogService.ShowWarning($"{positionName} 为 Magazine 系统默认位，不能删除。", "禁止删除默认位");
            return;
        }

        if (!UiGuards.Confirm(_dialogService, "删除 Magazine 位置", $"确定删除位置 {positionName} 吗？删除后需点击 Save 才会写入配置。"))
        {
            OperationStatus = "已取消删除 Magazine 位置";
            return;
        }

        if (!selected.DeleteSelectedPosition())
        {
            OperationStatus = $"位置 {positionName} 删除失败";
            return;
        }

        OperationStatus = $"Magazine {selected.Name} 位置 {positionName} 已删除（未保存）";
        (DeleteMagazinePositionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        RefreshViewModels(force: true);
    }

    private bool HasConfiguredAxesForMagazine()
    {
        var magazine = MagazineMonitor.SelectedMagazine;
        if (magazine is null) return false;
        var configuredAxes = new[] { magazine.XAxisNo, magazine.YAxisNo, magazine.ZAxisNo }.Where(no => no >= 0).ToList();
        if (configuredAxes.Count == 0) return false;
        return configuredAxes.All(no => _machine.Axes.Any(ax => ax.Id.Value == no));
    }

    private bool CanScanMagazine()
    {
        var magazine = MagazineMonitor.SelectedMagazine;
        if (magazine is null) return false;
        if (!HasConfiguredAxesForMagazine()) return false;
        if (magazine.ZAxisNo < 0) return false;
        if (magazine.CurrentLayerHasMaterialInputAddress < 0) return false;
        return magazine.Positions.Any(position => string.Equals(position.Kind, MagazinePositionKinds.InspectStart, StringComparison.OrdinalIgnoreCase));
    }

    private void TeachSelectedMagazinePosition()
    {
        var magazine = MagazineMonitor.SelectedMagazine;
        var selected = magazine?.SelectedPosition;
        if (magazine is null || selected is null)
        {
            OperationStatus = "请先选择 Magazine 及其位置点";
            return;
        }

        var configuredAxes = new[] { magazine.XAxisNo, magazine.YAxisNo, magazine.ZAxisNo }.Where(no => no >= 0).ToList();
        if (configuredAxes.Count == 0)
        {
            OperationStatus = $"{magazine.Name} 没有可 Teach 的轴，请先配置轴映射";
            return;
        }

        var missingAxes = configuredAxes.Where(no => _machine.Axes.All(ax => ax.Id.Value != no)).ToList();
        if (missingAxes.Count > 0)
        {
            var message = $"{magazine.Name} 存在未配置到运行时的轴: {string.Join(", ", missingAxes)}";
            OperationStatus = message;
            return;
        }

        try
        {
            if (magazine.XAxisNo >= 0) { var a = _machine.Axes.First(ax => ax.Id.Value == magazine.XAxisNo); selected.X = ToEngineeringPosition(a); }
            if (magazine.YAxisNo >= 0) { var a = _machine.Axes.First(ax => ax.Id.Value == magazine.YAxisNo); selected.Y = ToEngineeringPosition(a); }
            if (magazine.ZAxisNo >= 0) { var a = _machine.Axes.First(ax => ax.Id.Value == magazine.ZAxisNo); selected.Z = ToEngineeringPosition(a); }

            magazine.Refresh();
            OperationStatus = $"{magazine.Name}:{selected.Name} 当前坐标已 Teach";
            MagazineEventLogRecord($"{magazine.Name}:{selected.Name}", "Teach", $"{magazine.Name}:{selected.Name} teach completed");
        }
        catch (Exception ex)
        {
            OperationStatus = $"{magazine.Name}:{selected.Name} Teach 失败: {ex.Message}";
        }
    }

    private async Task MoveSelectedMagazinePositionAsync()
    {
        var magazine = MagazineMonitor.SelectedMagazine;
        var selected = magazine?.SelectedPosition;
        if (magazine is null || selected is null)
        {
            OperationStatus = "请先选择 Magazine 及其位置点";
            return;
        }

        var configuredAxes = new[] { magazine.XAxisNo, magazine.YAxisNo, magazine.ZAxisNo }.Where(no => no >= 0).ToList();
        if (configuredAxes.Count == 0)
        {
            OperationStatus = $"{magazine.Name} 没有可运动的轴，请先配置轴映射";
            return;
        }

        var missingAxes = configuredAxes.Where(no => _machine.Axes.All(ax => ax.Id.Value != no)).ToList();
        if (missingAxes.Count > 0)
        {
            var message = $"{magazine.Name} 存在未配置到运行时的轴: {string.Join(", ", missingAxes)}";
            OperationStatus = message;
            return;
        }

        var eventName = $"{magazine.Name}:{selected.Name}";
        OperationStatus = $"{eventName} 运动中...";
        MagazineEventLogRecord(eventName, "Command", $"{eventName} move started");

        try
        {
            // 多轴运动显式串行：底层 _multiStepLock 已保证单句柄下命令串行化，
            // 上层显式 await 与底层语义一致。
            if (magazine.XAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(MakeMoveDto(magazine.XAxisNo, selected.X));
                if (!r.Success)
                {
                    var message = $"{magazine.Name} X轴运动失败: {r.ErrorMessage}";
                    OperationStatus = message;
                    MagazineEventLogRecord(eventName, "Failed", message);
                    return;
                }
            }
            if (magazine.YAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(MakeMoveDto(magazine.YAxisNo, selected.Y));
                if (!r.Success)
                {
                    var message = $"{magazine.Name} Y轴运动失败: {r.ErrorMessage}";
                    OperationStatus = message;
                    MagazineEventLogRecord(eventName, "Failed", message);
                    return;
                }
            }
            if (magazine.ZAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(MakeMoveDto(magazine.ZAxisNo, selected.Z));
                if (!r.Success)
                {
                    var message = $"{magazine.Name} Z轴运动失败: {r.ErrorMessage}";
                    OperationStatus = message;
                    MagazineEventLogRecord(eventName, "Failed", message);
                    return;
                }
            }

            magazine.Refresh();
            OperationStatus = $"{eventName} 已定位至 ({selected.X:F2}, {selected.Y:F2}, {selected.Z:F2})";
            MagazineEventLogRecord(eventName, "Success", $"{eventName} move completed");
        }
        catch (Exception ex)
        {
            OperationStatus = $"{eventName} 运动异常: {ex.Message}";
            MagazineEventLogRecord(eventName, "Failed", $"{eventName} move failed: {ex.Message}");
        }
    }

    private async Task ScanMagazineAsync()
    {
        var magazine = MagazineMonitor.SelectedMagazine;
        if (magazine is null)
        {
            OperationStatus = "请先选择 Magazine";
            return;
        }

        var scanAlarmCode = $"SYS-MAGAZINE-{magazine.Name.ToUpperInvariant().Replace(" ", "-")}-SCAN-FAILED";
        var inspectStart = magazine.Positions.FirstOrDefault(position => string.Equals(position.Kind, MagazinePositionKinds.InspectStart, StringComparison.OrdinalIgnoreCase));
        if (inspectStart is null)
        {
            var message = $"{magazine.Name} 缺少检测起始位 InspectStart";
            OperationStatus = message;
            _machine.UpsertAlarm(scanAlarmCode, message, magazine.Name, "Magazine", "Error");
            return;
        }

        if (magazine.CurrentLayerHasMaterialInputAddress < 0)
        {
            var message = $"{magazine.Name} 未配置当前层检测 IN";
            OperationStatus = message;
            _machine.UpsertAlarm(scanAlarmCode, message, magazine.Name, "Magazine", "Error");
            return;
        }

        if (magazine.ZAxisNo < 0)
        {
            var message = $"{magazine.Name} 未配置 Z 轴，无法执行 Scan";
            OperationStatus = message;
            _machine.UpsertAlarm(scanAlarmCode, message, magazine.Name, "Magazine", "Error");
            return;
        }

        var configuredAxes = new[] { magazine.XAxisNo, magazine.YAxisNo, magazine.ZAxisNo }.Where(no => no >= 0).ToList();
        var missingAxes = configuredAxes.Where(no => _machine.Axes.All(ax => ax.Id.Value != no)).ToList();
        if (missingAxes.Count > 0)
        {
            var message = $"{magazine.Name} 存在未配置到运行时的轴: {string.Join(", ", missingAxes)}";
            OperationStatus = message;
            _machine.UpsertAlarm(scanAlarmCode, message, magazine.Name, "Magazine", "Error");
            return;
        }

        var sensor = _machine.IoPoints.FirstOrDefault(io => !io.IsOutput && io.Address == magazine.CurrentLayerHasMaterialInputAddress);
        if (sensor is null)
        {
            var message = $"{magazine.Name} 当前层检测 IN {magazine.CurrentLayerHasMaterialInputAddress} 不存在于运行时 IO";
            OperationStatus = message;
            _machine.UpsertAlarm(scanAlarmCode, message, magazine.Name, "Magazine", "Error");
            return;
        }

        var eventName = $"{magazine.Name}:{inspectStart.Name}";
        var totalLayers = Math.Max(1, magazine.LayerCount);
        magazine.ResetLayerStatuses();
        OperationStatus = $"{magazine.Name} Scan 中...";
        MagazineEventLogRecord(eventName, "Command", $"{eventName} scan started, layers={totalLayers}, step={magazine.LayerHeight:F2}, settling={magazine.ScanSettlingMs}ms");

        try
        {
            // 显式串行：底层 _multiStepLock 串行化复合命令
            if (magazine.XAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(MakeMoveDto(magazine.XAxisNo, inspectStart.X));
                if (!r.Success)
                {
                    var message = $"{magazine.Name} 定位检测起始位X轴失败: {r.ErrorMessage}";
                    OperationStatus = message;
                    _machine.UpsertAlarm(scanAlarmCode, message, magazine.Name, "Magazine", "Error");
                    MagazineEventLogRecord(eventName, "Failed", message);
                    return;
                }
            }
            if (magazine.YAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(MakeMoveDto(magazine.YAxisNo, inspectStart.Y));
                if (!r.Success)
                {
                    var message = $"{magazine.Name} 定位检测起始位Y轴失败: {r.ErrorMessage}";
                    OperationStatus = message;
                    _machine.UpsertAlarm(scanAlarmCode, message, magazine.Name, "Magazine", "Error");
                    MagazineEventLogRecord(eventName, "Failed", message);
                    return;
                }
            }
            if (magazine.ZAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(MakeMoveDto(magazine.ZAxisNo, inspectStart.Z));
                if (!r.Success)
                {
                    var message = $"{magazine.Name} 定位检测起始位Z轴失败: {r.ErrorMessage}";
                    OperationStatus = message;
                    _machine.UpsertAlarm(scanAlarmCode, message, magazine.Name, "Magazine", "Error");
                    MagazineEventLogRecord(eventName, "Failed", message);
                    return;
                }
            }

            await WaitForMagazineScanWindowAsync(magazine, sensor);
            magazine.Refresh();

            for (var layerIndex = 0; layerIndex < totalLayers; layerIndex++)
            {
                if (layerIndex > 0)
                {
                    var targetZ = inspectStart.Z + layerIndex * magazine.LayerHeight;
                    var zMove = await _motionAppService.MoveAbsoluteAsync(MakeMoveDto(magazine.ZAxisNo, targetZ));
                    if (!zMove.Success)
                    {
                        var message = $"{magazine.Name} 第{layerIndex + 1}层抬升失败: {zMove.ErrorMessage}";
                        OperationStatus = message;
                        _machine.UpsertAlarm(scanAlarmCode, message, magazine.Name, "Magazine", "Error");
                        MagazineEventLogRecord(eventName, "Failed", message);
                        return;
                    }

                    await WaitForMagazineScanWindowAsync(magazine, sensor);
                    magazine.Refresh();
                }

                var hasMaterial = sensor.Value;
                magazine.SetLayerScanStatus(layerIndex, hasMaterial);
                magazine.Refresh();
                var resultText = hasMaterial ? "有料" : "无料";
                MagazineEventLogRecord(eventName, "Scan", $"{magazine.Name} 第{layerIndex + 1}层检测结果: {resultText}");
            }

            if (_machine.ClearAlarm(scanAlarmCode))
            {
                MagazineEventLogRecord(eventName, "AlarmCleared", $"{eventName} cleared alarm {scanAlarmCode}");
            }

            OperationStatus = $"{magazine.Name} Scan 完成，共扫描 {totalLayers} 层";
            MagazineEventLogRecord(eventName, "Success", $"{eventName} scan completed, layers={totalLayers}");
        }
        catch (Exception ex)
        {
            var message = $"{magazine.Name} Scan 异常: {ex.Message}";
            OperationStatus = message;
            _machine.UpsertAlarm(scanAlarmCode, message, magazine.Name, "Magazine", "Error");
            MagazineEventLogRecord(eventName, "Failed", $"{eventName} scan failed: {ex.Message}");
        }
    }

    private async Task WaitForMagazineScanWindowAsync(MagazineItemViewModel magazine, IoPoint sensor)
    {
        await Task.Delay(100);
        magazine.Refresh();

        if (magazine.ScanSettlingMs <= 0 || sensor.Value) return;

        var startedAt = DateTime.UtcNow;
        while (DateTime.UtcNow - startedAt < TimeSpan.FromMilliseconds(magazine.ScanSettlingMs))
        {
            if (sensor.Value) return;
            await Task.Delay(20);
            magazine.Refresh();
        }
    }

    private MotionControl.Application.DTOs.MoveAxisCommandDto MakeMoveDto(int axisNo, double target)
    {
        var axis = _machine.Axes.First(a => a.Id.Value == axisNo);
        var vel = axis.WorkVelocity > 0 ? axis.WorkVelocity : 100;
        var acc = axis.Acceleration > 0 ? axis.Acceleration : 100;
        var dec = axis.Deceleration > 0 ? axis.Deceleration : 100;
        return new MotionControl.Application.DTOs.MoveAxisCommandDto(axisNo, target, vel, acc, dec);
    }

    private async Task SaveMagazineConfigAsync()
    {
        var configuredAxisNos = MagazineMonitor.Magazines
            .SelectMany(magazine => new[] { magazine.XAxisNo, magazine.YAxisNo, magazine.ZAxisNo })
            .Where(no => no >= 0)
            .Distinct()
            .ToList();
        var missingAxes = configuredAxisNos.Where(no => !_machine.Axes.Any(ax => ax.Id.Value == no)).ToList();
        if (missingAxes.Count > 0)
        {
            _dialogService.ShowWarning($"以下轴号未在 Axis Monitor 中配置，无法保存 Magazine：\n{string.Join(", ", missingAxes)}\n\n请先在 Axis Console 中添加这些轴。", "轴号未配置");
            OperationStatus = $"Magazine 保存失败：轴 {string.Join(", ", missingAxes)} 未配置";
            return;
        }

        var items = MagazineMonitor.Magazines.Select(magazine => new MotionControl.Infrastructure.Configuration.MagazineConfigItem
        {
            Name = magazine.Name,
            Description = magazine.Description,
            XAxisNo = magazine.XAxisNo,
            YAxisNo = magazine.YAxisNo,
            ZAxisNo = magazine.ZAxisNo,
            MaterialPresentInputAddress = magazine.MaterialPresentInputAddress,
            CurrentLayerHasMaterialInputAddress = magazine.CurrentLayerHasMaterialInputAddress,
            TrayKeyingInputAddress = magazine.TrayKeyingInputAddress,
            LayerCount = magazine.LayerCount,
            LayerHeight = magazine.LayerHeight,
            PickLiftHeight = magazine.PickLiftHeight,
            ScanSettlingMs = magazine.ScanSettlingMs,
            Positions = magazine.Positions.Select(position => position.ToConfig()).ToList()
        }).ToList();

        if (!UiGuards.Confirm(_dialogService, "保存 Magazine 配置", "确定覆盖当前 Magazine 配置到 appsettings.json 吗？"))
        {
            OperationStatus = "已取消保存 Magazine 配置";
            return;
        }

        try
        {
            await _magazineManagementAppService.SaveMagazinesAsync(items);
            await _magazineManagementAppService.LoadMagazinesAsync();
            OperationStatus = $"Magazine 配置已保存，共 {items.Count} 个料仓";
            RefreshViewModels(force: true);
        }
        catch (InvalidOperationException ex)
        {
            OperationStatus = ex.Message;
            _dialogService.ShowError(ex.Message, "Magazine 配置保存失败");
        }
    }

    private async Task LoadMagazineConfigAsync()
    {
        if (!UiGuards.Confirm(_dialogService, "加载 Magazine 配置", "确定从 appsettings.json 重新加载 Magazine 配置吗？未保存修改将丢失。"))
        {
            OperationStatus = "已取消加载 Magazine 配置";
            return;
        }

        await _magazineManagementAppService.LoadMagazinesAsync();
        MagazineMonitor.ReloadFromMachine();
        OperationStatus = "Magazine 配置已重新加载";
        RefreshViewModels(force: true);
    }

    private async Task SaveWorkHeadConfigAsync()
    {
        // 检查配置的轴号是否在 Axis Monitor 中存在
        var configuredAxisNos = WorkHeadMonitor.WorkHeads
            .SelectMany(wh => new[] { wh.XAxisNo, wh.YAxisNo, wh.ZAxisNo, wh.RAxisNo })
            .Where(no => no >= 0)
            .Distinct()
            .ToList();
        var missingAxes = configuredAxisNos.Where(no => !_machine.Axes.Any(ax => ax.Id.Value == no)).ToList();
        if (missingAxes.Count > 0)
        {
            _dialogService.ShowWarning($"以下轴号未在 Axis Monitor 中配置，无法保存 WorkHead：\n{string.Join(", ", missingAxes)}\n\n请先在 Axis Console 中添加这些轴。", "轴号未配置");
            OperationStatus = $"WorkHead 保存失败：轴 {string.Join(", ", missingAxes)} 未配置";
            return;
        }

        var items = WorkHeadMonitor.WorkHeads.Select(workHead => new MotionControl.Infrastructure.Configuration.WorkHeadConfigItem
        {
            Name = workHead.Name,
            Description = workHead.Description,
            XAxisNo = workHead.XAxisNo,
            YAxisNo = workHead.YAxisNo,
            ZAxisNo = workHead.ZAxisNo,
            RAxisNo = workHead.RAxisNo,
            VacuumOutputAddress = workHead.VacuumOutputAddress,
            BlowOutputAddress = workHead.BlowOutputAddress,
            VacuumInputAddress = workHead.VacuumInputAddress,
            GeneralOutputAddress1 = workHead.GeneralOutputAddress1,
            GeneralOutputAddress2 = workHead.GeneralOutputAddress2,
            GeneralInputAddress1 = workHead.GeneralInputAddress1,
            GeneralInputAddress2 = workHead.GeneralInputAddress2,
            VacuumTimeoutMs = workHead.VacuumTimeoutMs,
            SafeZ = workHead.SafeZ,
            Positions = workHead.Positions.Select(p => new MotionControl.Infrastructure.Configuration.WorkHeadPositionConfigItem
            {
                Name = p.Name,
                Description = p.Description,
                X = p.X,
                Y = p.Y,
                Z = p.Z,
                R = p.R
            }).ToList()
        }).ToList();

        if (!UiGuards.Confirm(_dialogService, "保存 WorkHead 配置", "确定覆盖当前 WorkHead 配置到 appsettings.json 吗？"))
        {
            OperationStatus = "已取消保存 WorkHead 配置";
            return;
        }

        try
        {
            await _workHeadManagementAppService.SaveWorkHeadsAsync(items);
            await _workHeadManagementAppService.LoadWorkHeadsAsync();
            OnPropertyChanged(nameof(WorkHeadNames));
            OperationStatus = $"WorkHead 配置已保存，共 {items.Count} 个工作头";
            RefreshViewModels(force: true);
        }
        catch (InvalidOperationException ex)
        {
            OperationStatus = ex.Message;
            _dialogService.ShowError(ex.Message, "WorkHead 配置保存失败");
        }
    }

    private async Task LoadWorkHeadConfigAsync()
    {
        if (!UiGuards.Confirm(_dialogService, "加载 WorkHead 配置", "确定从 appsettings.json 重新加载 WorkHead 配置吗？未保存修改将丢失。"))
        {
            OperationStatus = "已取消加载 WorkHead 配置";
            return;
        }

        await _workHeadManagementAppService.LoadWorkHeadsAsync();
        WorkHeadMonitor.ReloadFromMachine();
        OnPropertyChanged(nameof(WorkHeadNames));
        OperationStatus = "WorkHead 配置已重新加载";
        RefreshViewModels(force: true);
    }

    private Task AddPositionSetupAsync()
    {
        // 仅在内存中新增，Save 时统一落盘
        // 新对象默认带 1 个空位置点（满足"至少一个位置点"的校验约束）
        var index = PositionSetupMonitor.Items.Count + 1;
        var item = new MotionControl.Infrastructure.Configuration.PositionSetupConfigItem
        {
            Name = $"PositionSetup {index}",
            Description = string.Empty,
            SafeZ = 0,
            XxAxisNo = -1,
            XAxisNo = -1,
            YAxisNo = -1,
            ZAxisNo = -1,
            UAxisNo = -1,
            VAxisNo = -1,
            WAxisNo = -1,
            Positions = new List<MotionControl.Infrastructure.Configuration.PositionSetupPositionConfigItem>
            {
                new() { Name = "Position 1", Description = string.Empty }
            }
        };

        PositionSetupMonitor.Add(item);
        OperationStatus = $"位置设定 {item.Name} 已新增（含默认位置点，未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private Task DeleteSelectedPositionSetupAsync()
    {
        var selected = PositionSetupMonitor.SelectedItem;
        if (selected is null) return Task.CompletedTask;

        if (!UiGuards.Confirm(_dialogService, "删除位置设定", $"确定删除位置设定 {selected.Name} 吗？删除后需点击 Save 才会写入配置。"))
        {
            OperationStatus = "已取消删除位置设定";
            return Task.CompletedTask;
        }

        PositionSetupMonitor.RemoveSelected();
        OperationStatus = $"位置设定 {selected.Name} 已删除（未保存）";
        RefreshViewModels(force: true);
        return Task.CompletedTask;
    }

    private void AddPositionSetupPosition()
    {
        if (PositionSetupMonitor.SelectedItem is null) return;
        PositionSetupMonitor.SelectedItem.AddPosition();
        OperationStatus = $"位置 {PositionSetupMonitor.SelectedItem.Positions.Last().Name} 已添加";
        RefreshViewModels(force: true);
    }

    private void DeleteSelectedPositionSetupPosition()
    {
        if (PositionSetupMonitor.SelectedItem?.SelectedPosition is null) return;
        var setupItem = PositionSetupMonitor.SelectedItem;
        var selectedPosition = setupItem.SelectedPosition;
        if (!UiGuards.Confirm(_dialogService, "删除位置点", $"确定删除 {setupItem.Name} 下的位置 {selectedPosition.Name} 吗？删除后需点击 Save 才会写入配置。"))
        {
            OperationStatus = "已取消删除位置点";
            return;
        }

        setupItem.DeleteSelectedPosition();
        OperationStatus = "位置已删除（未保存）";
        RefreshViewModels(force: true);
    }

    private async Task SavePositionSetupConfigAsync()
    {
        // 检查配置的轴号是否在 Axis Monitor 中存在
        var configuredAxisNos = PositionSetupMonitor.Items
            .SelectMany(setup => new[] { setup.XxAxisNo, setup.XAxisNo, setup.YAxisNo, setup.ZAxisNo, setup.UAxisNo, setup.VAxisNo, setup.WAxisNo })
            .Where(no => no >= 0)
            .Distinct()
            .ToList();
        var missingAxes = configuredAxisNos.Where(no => !_machine.Axes.Any(ax => ax.Id.Value == no)).ToList();
        if (missingAxes.Count > 0)
        {
            _dialogService.ShowWarning($"以下轴号未在 Axis Monitor 中配置，无法保存位置设定：\n{string.Join(", ", missingAxes)}\n\n请先在 Axis Console 中添加这些轴。", "轴号未配置");
            OperationStatus = $"位置设定保存失败：轴 {string.Join(", ", missingAxes)} 未配置";
            return;
        }

        var items = PositionSetupMonitor.ToConfigs();

        if (!UiGuards.Confirm(_dialogService, "保存位置设定配置", "确定覆盖当前位置设定配置到 appsettings.json 吗？"))
        {
            OperationStatus = "已取消保存位置设定配置";
            return;
        }

        try
        {
            await _positionSetupManagementAppService.SavePositionsAsync(items);

            // 保存后回读，保证排序、绑定对象、选中项一致
            var reloaded = await _positionSetupManagementAppService.LoadPositionsAsync();
            PositionSetupMonitor.Load(reloaded);

            OperationStatus = $"位置设定配置已保存，共 {items.Count} 个位置";
            RefreshViewModels(force: true);
        }
        catch (InvalidOperationException ex)
        {
            OperationStatus = ex.Message;
            _dialogService.ShowError(ex.Message, "位置设定配置保存失败");
        }
    }

    private async Task LoadPositionSetupConfigAsync(CancellationToken cancellationToken = default)
    {
        if (!UiGuards.Confirm(_dialogService, "加载位置设定配置", "确定从 appsettings.json 重新加载位置设定配置吗？未保存修改将丢失。"))
        {
            OperationStatus = "已取消加载位置设定配置";
            return;
        }

        await LoadPositionSetupConfigOnStartupAsync(cancellationToken);
    }

    private async Task LoadPositionSetupConfigOnStartupAsync(CancellationToken cancellationToken = default)
    {
        var items = await _positionSetupManagementAppService.LoadPositionsAsync(cancellationToken);
        PositionSetupMonitor.Load(items);
        OperationStatus = $"位置设定配置已加载，共 {items.Count} 个位置";
    }

    private void TeachSelectedPositionSetup()
    {
        var setupItem = PositionSetupMonitor.SelectedItem;
        var selectedPos = setupItem?.SelectedPosition;
        if (setupItem is null || selectedPos is null)
        {
            OperationStatus = "请先选择位置设定对象及其位置点";
            return;
        }

        try
        {
            var configuredAxes = new[]
            {
                setupItem.XxAxisNo,
                setupItem.XAxisNo,
                setupItem.YAxisNo,
                setupItem.ZAxisNo,
                setupItem.UAxisNo,
                setupItem.VAxisNo,
                setupItem.WAxisNo
            }.Where(axisNo => axisNo >= 0).Distinct().ToList();

            if (configuredAxes.Count == 0)
            {
                OperationStatus = $"{setupItem.Name} 没有可 Teach 的轴，请先配置轴映射";
                PositionSetupEventLogRecord(setupItem.Name + ":" + selectedPos.Name, "Skipped", $"{setupItem.Name}:{selectedPos.Name} teach skipped, no configured axes");
                return;
            }

            var missingAxes = configuredAxes.Where(axisNo => _machine.Axes.All(axis => axis.Id.Value != axisNo)).ToList();
            if (missingAxes.Count > 0)
            {
                var message = $"{setupItem.Name} 存在未配置到运行时的轴: {string.Join(", ", missingAxes)}";
                OperationStatus = message;
                PositionSetupEventLogRecord(setupItem.Name + ":" + selectedPos.Name, "Failed", message);
                return;
            }

            // 使用父对象的轴映射读取当前位置，写入子位置点
            if (setupItem.XxAxisNo >= 0) { var a = _machine.Axes.First(x => x.Id.Value == setupItem.XxAxisNo); selectedPos.XxPosition = ToEngineeringPosition(a); }
            if (setupItem.XAxisNo >= 0) { var a = _machine.Axes.First(x => x.Id.Value == setupItem.XAxisNo); selectedPos.XPosition = ToEngineeringPosition(a); }
            if (setupItem.YAxisNo >= 0) { var a = _machine.Axes.First(x => x.Id.Value == setupItem.YAxisNo); selectedPos.YPosition = ToEngineeringPosition(a); }
            if (setupItem.ZAxisNo >= 0) { var a = _machine.Axes.First(x => x.Id.Value == setupItem.ZAxisNo); selectedPos.ZPosition = ToEngineeringPosition(a); }
            if (setupItem.UAxisNo >= 0) { var a = _machine.Axes.First(x => x.Id.Value == setupItem.UAxisNo); selectedPos.UPosition = ToEngineeringPosition(a); }
            if (setupItem.VAxisNo >= 0) { var a = _machine.Axes.First(x => x.Id.Value == setupItem.VAxisNo); selectedPos.VPosition = ToEngineeringPosition(a); }
            if (setupItem.WAxisNo >= 0) { var a = _machine.Axes.First(x => x.Id.Value == setupItem.WAxisNo); selectedPos.WPosition = ToEngineeringPosition(a); }

            // 刷新选中项所有绑定 + 记录事件
            setupItem.Refresh();
            var eventName = $"{setupItem.Name}:{selectedPos.Name}";
            OperationStatus = $"{eventName} 当前坐标已 Teach";
            PositionSetupEventLogRecord(eventName, "Teach", $"{eventName} teach completed");
        }
        catch (Exception ex)
        {
            var eventName = $"{setupItem.Name}:{selectedPos.Name}";
            OperationStatus = $"{eventName} Teach 失败: {ex.Message}";
            PositionSetupEventLogRecord(eventName, "Failed", $"{eventName} teach failed: {ex.Message}");
        }
    }

    private async Task MoveSelectedPositionSetupAsync()
    {
        var setupItem = PositionSetupMonitor.SelectedItem;
        var selectedPos = setupItem?.SelectedPosition;
        if (setupItem is null || selectedPos is null)
        {
            OperationStatus = "请先选择位置设定对象及其位置点";
            return;
        }

        try
        {
            var configuredAxes = new[]
            {
                setupItem.XxAxisNo,
                setupItem.XAxisNo,
                setupItem.YAxisNo,
                setupItem.ZAxisNo,
                setupItem.UAxisNo,
                setupItem.VAxisNo,
                setupItem.WAxisNo
            }.Where(axisNo => axisNo >= 0).Distinct().ToList();

            if (configuredAxes.Count == 0)
            {
                var msg = $"{setupItem.Name} 没有可运动的轴，请先配置轴映射";
                OperationStatus = msg;
                PositionSetupEventLogRecord(setupItem.Name + ":" + selectedPos.Name, "Skipped", $"{setupItem.Name}:{selectedPos.Name} move skipped, no configured axes");
                return;
            }

            var missingAxes = configuredAxes.Where(axisNo => _machine.Axes.All(axis => axis.Id.Value != axisNo)).ToList();
            if (missingAxes.Count > 0)
            {
                var message = $"{setupItem.Name} 存在未配置到运行时的轴: {string.Join(", ", missingAxes)}";
                OperationStatus = message;
                _commandFeedbackRuntimeState.AddFailed("PositionSetupMove", message: message);
                PositionSetupEventLogRecord(setupItem.Name + ":" + selectedPos.Name, "Failed", message);
                return;
            }

            if (setupItem.ZAxisNo >= 0 && setupItem.SafeZ < selectedPos.ZPosition)
            {
                var message = $"{setupItem.Name} 的 SafeZ 不能低于目标 Z";
                OperationStatus = message;
                _commandFeedbackRuntimeState.AddFailed("PositionSetupMove", message: message);
                PositionSetupEventLogRecord(setupItem.Name + ":" + selectedPos.Name, "Failed", message);
                return;
            }

            MotionControl.Application.DTOs.MoveAxisCommandDto BuildMove(int axisNo, double target)
            {
                var axis = _machine.Axes.First(a => a.Id.Value == axisNo);
                var vel = axis.WorkVelocity > 0 ? axis.WorkVelocity : 100;
                var acc = axis.Acceleration > 0 ? axis.Acceleration : 100;
                var dec = axis.Deceleration > 0 ? axis.Deceleration : 100;
                return new MotionControl.Application.DTOs.MoveAxisCommandDto(axisNo, target, vel, acc, dec);
            }

            var eventName = $"{setupItem.Name}:{selectedPos.Name}";
            _commandFeedbackRuntimeState.AddStarted("PositionSetupMove", message: $"{eventName} started");
            PositionSetupEventLogRecord(eventName, "Command", $"{eventName} move started");

            // 安全顺序: 先抬 Z 到 SafeZ，再其它轴串行，最后 Z 到目标。
            // 所有运动底层通过 _multiStepLock 串行化，上层显式 await 与之一致。
            if (setupItem.ZAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(BuildMove(setupItem.ZAxisNo, setupItem.SafeZ));
                if (!r.Success)
                {
                    var message = $"{setupItem.Name} Z轴抬升失败: {r.ErrorMessage}";
                    OperationStatus = message;
                    _machine.UpsertAlarm("SYS-POSITIONSETUP-MOVE-FAILED", message, setupItem.Name, "PositionSetup", "Error");
                    _commandFeedbackRuntimeState.AddFailed("PositionSetupMove", message: message);
                    PositionSetupEventLogRecord(eventName, "Failed", message);
                    return;
                }
            }

            if (setupItem.XxAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(BuildMove(setupItem.XxAxisNo, selectedPos.XxPosition));
                if (!r.Success)
                {
                    var msg = $"{setupItem.Name} Xx轴运动失败: {r.ErrorMessage}";
                    OperationStatus = msg;
                    _machine.UpsertAlarm("SYS-POSITIONSETUP-MOVE-FAILED", msg, setupItem.Name, "PositionSetup", "Error");
                    _commandFeedbackRuntimeState.AddFailed("PositionSetupMove", message: msg);
                    PositionSetupEventLogRecord(eventName, "Failed", msg);
                    return;
                }
            }
            if (setupItem.XAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(BuildMove(setupItem.XAxisNo, selectedPos.XPosition));
                if (!r.Success)
                {
                    var msg = $"{setupItem.Name} X轴运动失败: {r.ErrorMessage}";
                    OperationStatus = msg;
                    _machine.UpsertAlarm("SYS-POSITIONSETUP-MOVE-FAILED", msg, setupItem.Name, "PositionSetup", "Error");
                    _commandFeedbackRuntimeState.AddFailed("PositionSetupMove", message: msg);
                    PositionSetupEventLogRecord(eventName, "Failed", msg);
                    return;
                }
            }
            if (setupItem.YAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(BuildMove(setupItem.YAxisNo, selectedPos.YPosition));
                if (!r.Success)
                {
                    var msg = $"{setupItem.Name} Y轴运动失败: {r.ErrorMessage}";
                    OperationStatus = msg;
                    _machine.UpsertAlarm("SYS-POSITIONSETUP-MOVE-FAILED", msg, setupItem.Name, "PositionSetup", "Error");
                    _commandFeedbackRuntimeState.AddFailed("PositionSetupMove", message: msg);
                    PositionSetupEventLogRecord(eventName, "Failed", msg);
                    return;
                }
            }
            if (setupItem.UAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(BuildMove(setupItem.UAxisNo, selectedPos.UPosition));
                if (!r.Success)
                {
                    var msg = $"{setupItem.Name} U轴运动失败: {r.ErrorMessage}";
                    OperationStatus = msg;
                    _machine.UpsertAlarm("SYS-POSITIONSETUP-MOVE-FAILED", msg, setupItem.Name, "PositionSetup", "Error");
                    _commandFeedbackRuntimeState.AddFailed("PositionSetupMove", message: msg);
                    PositionSetupEventLogRecord(eventName, "Failed", msg);
                    return;
                }
            }
            if (setupItem.VAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(BuildMove(setupItem.VAxisNo, selectedPos.VPosition));
                if (!r.Success)
                {
                    var msg = $"{setupItem.Name} V轴运动失败: {r.ErrorMessage}";
                    OperationStatus = msg;
                    _machine.UpsertAlarm("SYS-POSITIONSETUP-MOVE-FAILED", msg, setupItem.Name, "PositionSetup", "Error");
                    _commandFeedbackRuntimeState.AddFailed("PositionSetupMove", message: msg);
                    PositionSetupEventLogRecord(eventName, "Failed", msg);
                    return;
                }
            }
            if (setupItem.WAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(BuildMove(setupItem.WAxisNo, selectedPos.WPosition));
                if (!r.Success)
                {
                    var msg = $"{setupItem.Name} W轴运动失败: {r.ErrorMessage}";
                    OperationStatus = msg;
                    _machine.UpsertAlarm("SYS-POSITIONSETUP-MOVE-FAILED", msg, setupItem.Name, "PositionSetup", "Error");
                    _commandFeedbackRuntimeState.AddFailed("PositionSetupMove", message: msg);
                    PositionSetupEventLogRecord(eventName, "Failed", msg);
                    return;
                }
            }

            if (setupItem.ZAxisNo >= 0)
            {
                var r = await _motionAppService.MoveAbsoluteAsync(BuildMove(setupItem.ZAxisNo, selectedPos.ZPosition));
                if (!r.Success)
                {
                    var message = $"{setupItem.Name} Z轴定位失败: {r.ErrorMessage}";
                    OperationStatus = message;
                    _machine.UpsertAlarm("SYS-POSITIONSETUP-MOVE-FAILED", message, setupItem.Name, "PositionSetup", "Error");
                    _commandFeedbackRuntimeState.AddFailed("PositionSetupMove", message: message);
                    PositionSetupEventLogRecord(eventName, "Failed", message);
                    return;
                }
            }

            setupItem.Refresh();
            OperationStatus = $"{eventName} 已执行定位";
            _commandFeedbackRuntimeState.AddSucceeded("PositionSetupMove", message: $"{eventName} completed");
            PositionSetupEventLogRecord(eventName, "Success", $"{eventName} move completed");
        }
        catch (Exception ex)
        {
            var eventName = $"{setupItem.Name}:{selectedPos.Name}";
            var message = $"{eventName} 运动失败: {ex.Message}";
            OperationStatus = message;
            _machine.UpsertAlarm("SYS-POSITIONSETUP-MOVE-FAILED", message, setupItem.Name, "PositionSetup", "Error");
            _commandFeedbackRuntimeState.AddFailed("PositionSetupMove", message: $"{eventName} failed: {ex.Message}");
            PositionSetupEventLogRecord(eventName, "Failed", $"{eventName} move failed: {ex.Message}");
        }
    }

    private async Task LoadIoConfigAsync()
    {
        if (!UiGuards.Confirm(_dialogService, "加载 IO 配置", "确定从 appsettings.json 重新加载 IO 配置吗？未保存修改将丢失。"))
        {
            OperationStatus = "已取消加载 IO 配置";
            return;
        }

        await _ioManagementAppService.LoadIoPointsAsync();
        IoMonitor.ReloadFromMachine();
        _ioMonitorCoordinator.AfterLoadOrReload();
        OperationStatus = "IO 配置已重新加载";
        RefreshViewModels(force: true);
    }


    private void RefreshCylinderStates()
    {
        var utcNow = DateTime.UtcNow;
        foreach (var cylinder in _machine.Cylinders)
        {
            var previousState = cylinder.State;
            var previousPendingCommand = cylinder.PendingCommand;
            var extendSensorOn = _machine.IoPoints.FirstOrDefault(io => !io.IsOutput && io.Address == cylinder.ExtendSensorInputAddress)?.Value ?? false;
            var retractSensorOn = _machine.IoPoints.FirstOrDefault(io => !io.IsOutput && io.Address == cylinder.RetractSensorInputAddress)?.Value ?? false;
            var extendOutputOn = _machine.IoPoints.FirstOrDefault(io => io.IsOutput && io.Address == cylinder.ExtendOutputAddress)?.Value ?? false;
            var retractOutputOn = _machine.IoPoints.FirstOrDefault(io => io.IsOutput && io.Address == cylinder.RetractOutputAddress)?.Value ?? false;
            cylinder.UpdateState(extendSensorOn, retractSensorOn, extendOutputOn, retractOutputOn);

            if (previousPendingCommand == CylinderCommandType.Extend && cylinder.State == CylinderState.Extended)
            {
                CylinderEventLogRecord(cylinder.Name, "Success", $"{cylinder.Name} extended successfully");
            }
            else if (previousPendingCommand == CylinderCommandType.Retract && cylinder.State == CylinderState.Retracted)
            {
                CylinderEventLogRecord(cylinder.Name, "Success", $"{cylinder.Name} retracted successfully");
            }
            else if (previousState != CylinderState.Conflict && cylinder.State == CylinderState.Conflict)
            {
                CylinderEventLogRecord(cylinder.Name, "Conflict", $"{cylinder.Name} sensor conflict detected");
            }

            var timeoutAlarmCode = $"CYL-{cylinder.Name}-TIMEOUT";
            if (cylinder.IsActionTimedOut(utcNow))
            {
                if (_machine.UpsertAlarm(timeoutAlarmCode, $"Cylinder {cylinder.Name} {cylinder.PendingCommand} timeout ({cylinder.ActionTimeoutMs} ms)", cylinder.Name, "Cylinder", "Error"))
                {
                    CylinderEventLogRecord(cylinder.Name, "Timeout", $"{cylinder.Name} {cylinder.PendingCommand} timeout ({cylinder.ActionTimeoutMs} ms)");
                }
            }
            else if (_machine.ClearAlarm(timeoutAlarmCode))
            {
                CylinderEventLogRecord(cylinder.Name, "Recovered", $"{cylinder.Name} timeout cleared");
            }

            var conflictAlarmCode = $"CYL-{cylinder.Name}-SENSOR-CONFLICT";
            if (cylinder.State == CylinderState.Conflict)
            {
                if (_machine.UpsertAlarm(conflictAlarmCode, $"Cylinder {cylinder.Name} sensor conflict: extend/retract DI both ON", cylinder.Name, "Cylinder", "Error"))
                {
                    CylinderEventLogRecord(cylinder.Name, "Conflict", $"{cylinder.Name} sensor conflict");
                }
            }
            else if (_machine.ClearAlarm(conflictAlarmCode))
            {
                CylinderEventLogRecord(cylinder.Name, "Recovered", $"{cylinder.Name} sensor conflict cleared");
            }
        }
    }

    private void CylinderEventLogRecord(string cylinderName, string eventType, string message)
    {
        _cylinderEventRuntimeState.Add(new CylinderEventRecord
        {
            CylinderName = cylinderName,
            EventType = eventType,
            Message = message
        });
    }

    private static double ToEngineeringPosition(MotionControl.Domain.Entities.Axis axis)
    {
        var pulseEquivalent = axis.PulseEquivalent > 0 ? axis.PulseEquivalent : 1000;
        return axis.CurrentPosition / pulseEquivalent;
    }

    private void PositionSetupEventLogRecord(string positionName, string eventType, string message)
    {
        _positionSetupEventRuntimeState.Add(new PositionSetupEventRecord
        {
            PositionName = positionName,
            EventType = eventType,
            Message = message
        });
        PositionSetupEventLog.Refresh();
    }

    private void MagazineEventLogRecord(string magazinePositionName, string eventType, string message)
    {
        _magazineEventRuntimeState.Add(new MagazineEventRecord
        {
            MagazineName = magazinePositionName,
            EventType = eventType,
            Message = message
        });
        MagazineEventLog.Refresh();
    }

    public void ReportStatus(string message)
    {
        OperationStatus = message;
    }

    private bool CanControlAxisCommands()
        => _controllerRuntimeState.IsConnected && _machine.CurrentState != SystemState.EmergencyStop;

    private bool CanWriteAxisConfiguration()
        => AxisMonitor.SelectedAxis is not null && _machine.CurrentState != SystemState.EmergencyStop;

    private bool CanAccessControllerParameters()
        => AxisMonitor.SelectedAxis is not null && _controllerRuntimeState.IsConnected && _machine.CurrentState != SystemState.EmergencyStop;

    private bool CanWriteIoOutputs()
        => _controllerRuntimeState.IsConnected && _machine.CurrentState != SystemState.EmergencyStop;

    private bool CanEditIoConfiguration()
        => _machine.CurrentState != SystemState.EmergencyStop;

    private static string GetBeijingTimeString()
    {
        var utcNow = DateTime.UtcNow;
        var beijingNow = utcNow + TimeSpan.FromHours(8);
        return beijingNow.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
