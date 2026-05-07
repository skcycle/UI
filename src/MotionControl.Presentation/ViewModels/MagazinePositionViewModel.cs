using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using MotionControl.Domain.Entities;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Presentation.ViewModels;

public sealed class MagazinePositionViewModel : INotifyPropertyChanged
{
    private readonly MagazinePosition _position;

    public MagazinePositionViewModel(MagazinePosition position)
    {
        _position = position;
    }

    public string Name
    {
        get => _position.Name;
        set
        {
            if (IsSystemDefault) return;
            if (_position.Name == value) return;
            _position.Name = value;
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => _position.Description;
        set { if (_position.Description == value) return; _position.Description = value; OnPropertyChanged(); }
    }

    public string Kind
    {
        get => _position.Kind;
        set
        {
            if (_position.Kind == value) return;
            _position.Kind = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSystemDefault));
            OnPropertyChanged(nameof(BadgeText));
            OnPropertyChanged(nameof(BadgeBrush));
        }
    }

    public double X
    {
        get => _position.X;
        set { if (_position.X == value) return; _position.X = value; OnPropertyChanged(); }
    }

    public double Y
    {
        get => _position.Y;
        set { if (_position.Y == value) return; _position.Y = value; OnPropertyChanged(); }
    }

    public double Z
    {
        get => _position.Z;
        set { if (_position.Z == value) return; _position.Z = value; OnPropertyChanged(); }
    }

    public bool IsSystemDefault =>
        string.Equals(_position.Kind, MagazinePositionKinds.PickStart, StringComparison.OrdinalIgnoreCase)
        || string.Equals(_position.Kind, MagazinePositionKinds.InspectStart, StringComparison.OrdinalIgnoreCase);

    public string BadgeText => Kind switch
    {
        MagazinePositionKinds.PickStart => "取料起始位",
        MagazinePositionKinds.InspectStart => "检测起始位",
        _ => string.Empty
    };

    public Brush BadgeBrush => Kind switch
    {
        MagazinePositionKinds.PickStart => new SolidColorBrush(Color.FromRgb(74, 158, 255)),
        MagazinePositionKinds.InspectStart => new SolidColorBrush(Color.FromRgb(155, 89, 182)),
        _ => Brushes.Transparent
    };

    public MagazinePositionConfigItem ToConfig()
    {
        return new MagazinePositionConfigItem
        {
            Name = _position.Name,
            Description = _position.Description,
            Kind = _position.Kind,
            X = _position.X,
            Y = _position.Y,
            Z = _position.Z
        };
    }

    public MagazinePosition ToDomain() => _position;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
