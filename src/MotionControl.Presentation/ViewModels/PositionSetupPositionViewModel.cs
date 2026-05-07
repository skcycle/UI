using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// 封装 PositionSetupPositionConfigItem，提供 INotifyPropertyChanged。
/// </summary>
public sealed class PositionSetupPositionViewModel : INotifyPropertyChanged
{
    private readonly PositionSetupPositionConfigItem _item;

    public PositionSetupPositionViewModel(PositionSetupPositionConfigItem item)
    {
        _item = item;
    }

    public PositionSetupPositionConfigItem ToConfig() => _item;

    public string Name { get => _item.Name; set { if (_item.Name == value) return; _item.Name = value; OnPropertyChanged(); } }
    public string Description { get => _item.Description; set { if (_item.Description == value) return; _item.Description = value; OnPropertyChanged(); } }
    public double XxPosition { get => _item.XxPosition; set { if (_item.XxPosition == value) return; _item.XxPosition = value; OnPropertyChanged(); } }
    public double XPosition { get => _item.XPosition; set { if (_item.XPosition == value) return; _item.XPosition = value; OnPropertyChanged(); } }
    public double YPosition { get => _item.YPosition; set { if (_item.YPosition == value) return; _item.YPosition = value; OnPropertyChanged(); } }
    public double ZPosition { get => _item.ZPosition; set { if (_item.ZPosition == value) return; _item.ZPosition = value; OnPropertyChanged(); } }
    public double UPosition { get => _item.UPosition; set { if (_item.UPosition == value) return; _item.UPosition = value; OnPropertyChanged(); } }
    public double VPosition { get => _item.VPosition; set { if (_item.VPosition == value) return; _item.VPosition = value; OnPropertyChanged(); } }
    public double WPosition { get => _item.WPosition; set { if (_item.WPosition == value) return; _item.WPosition = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
