using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Control.Services;
using MotionControl.Domain.Entities;

namespace MotionControl.Presentation.ViewModels;

public sealed class IoMonitorViewModel : INotifyPropertyChanged
{
    private readonly Machine _machine;
    private readonly IoControlService _ioControlService;
    private readonly CommandFeedbackRuntimeState _commandFeedbackRuntimeState;
    private readonly Func<bool> _canToggleOutput;
    private IoPointViewModel? _selectedInput;
    private IoPointViewModel? _selectedOutput;

    public IoMonitorViewModel(Machine machine, IoControlService ioControlService, CommandFeedbackRuntimeState commandFeedbackRuntimeState, Func<bool> canToggleOutput)
    {
        _machine = machine;
        _ioControlService = ioControlService;
        _commandFeedbackRuntimeState = commandFeedbackRuntimeState;
        _canToggleOutput = canToggleOutput;
        Inputs = BuildInputs();
        Outputs = BuildOutputs();
    }

    public ObservableCollection<IoPointViewModel> Inputs { get; }
    public ObservableCollection<IoPointViewModel> Outputs { get; }

    public IoPointViewModel? SelectedInput
    {
        get => _selectedInput;
        set
        {
            if (_selectedInput == value) return;
            _selectedInput = value;
            OnPropertyChanged();
        }
    }

    public IoPointViewModel? SelectedOutput
    {
        get => _selectedOutput;
        set
        {
            if (_selectedOutput == value) return;
            _selectedOutput = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshAll()
    {
        foreach (var input in Inputs)
        {
            input.Refresh();
        }

        foreach (var output in Outputs)
        {
            output.Refresh();
            output.RefreshCommandState();
        }
    }

    public void ReloadFromMachine()
    {
        Inputs.Clear();
        foreach (var input in BuildInputs())
        {
            Inputs.Add(input);
        }

        Outputs.Clear();
        foreach (var output in BuildOutputs())
        {
            Outputs.Add(output);
        }
    }

    public void AddIoPoint(IoPoint ioPoint)
    {
        var viewModel = new IoPointViewModel(ioPoint, _ioControlService, _commandFeedbackRuntimeState, _canToggleOutput, _machine);
        if (ioPoint.IsOutput)
        {
            Outputs.Add(viewModel);
            SelectedOutput = viewModel;
        }
        else
        {
            Inputs.Add(viewModel);
            SelectedInput = viewModel;
        }
    }

    public void RemoveIoPoint(bool isOutput, int address)
    {
        var collection = isOutput ? Outputs : Inputs;
        var existing = collection.FirstOrDefault(io => io.Address == address);
        if (existing is null)
        {
            return;
        }

        var wasSelected = isOutput ? SelectedOutput == existing : SelectedInput == existing;
        collection.Remove(existing);
        if (!wasSelected)
        {
            return;
        }

        if (isOutput)
        {
            SelectedOutput = Outputs.FirstOrDefault();
        }
        else
        {
            SelectedInput = Inputs.FirstOrDefault();
        }
    }

    public void SelectIoPoint(IoPointViewModel? ioPoint)
    {
        if (ioPoint is null)
        {
            return;
        }

        if (ioPoint.IsOutput)
        {
            SelectedOutput = ioPoint;
        }
        else
        {
            SelectedInput = ioPoint;
        }
    }

    public IReadOnlyList<IoPointViewModel> GetAllIoPoints()
        => Inputs.Concat(Outputs).ToList();

    private ObservableCollection<IoPointViewModel> BuildInputs()
        => new(_machine.IoPoints.Where(io => !io.IsOutput).Select(io => new IoPointViewModel(io, _ioControlService, _commandFeedbackRuntimeState, _canToggleOutput, _machine)));

    private ObservableCollection<IoPointViewModel> BuildOutputs()
        => new(_machine.IoPoints.Where(io => io.IsOutput).Select(io => new IoPointViewModel(io, _ioControlService, _commandFeedbackRuntimeState, _canToggleOutput, _machine)));

    private void SyncCollections()
    {
        SyncCollection(Inputs, _machine.IoPoints.Where(io => !io.IsOutput).ToList());
        SyncCollection(Outputs, _machine.IoPoints.Where(io => io.IsOutput).ToList());
    }

    private void SyncCollection(ObservableCollection<IoPointViewModel> collection, IReadOnlyList<IoPoint> source)
    {
        var sourceAddresses = source.Select(io => io.Address).ToHashSet();
        for (var index = collection.Count - 1; index >= 0; index--)
        {
            if (!sourceAddresses.Contains(collection[index].Address))
            {
                collection.RemoveAt(index);
            }
        }

        foreach (var ioPoint in source)
        {
            var existing = collection.FirstOrDefault(item => item.Address == ioPoint.Address);
            if (existing is null)
            {
                collection.Add(new IoPointViewModel(ioPoint, _ioControlService, _commandFeedbackRuntimeState, _canToggleOutput, _machine));
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
