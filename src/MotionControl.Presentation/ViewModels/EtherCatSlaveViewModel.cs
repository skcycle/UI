using MotionControl.Device.Abstractions.Models;

namespace MotionControl.Presentation.ViewModels;

public sealed class EtherCatSlaveViewModel : IEquatable<EtherCatSlaveViewModel>
{
    public EtherCatSlaveViewModel(EtherCatSlaveStatus slaveStatus)
    {
        SlaveNo = slaveStatus.SlaveNo;
        Name = slaveStatus.Name;
        State = slaveStatus.State;
        ModuleType = slaveStatus.ModuleType;
        ModuleState = slaveStatus.ModuleState;
        TopologyPath = slaveStatus.TopologyPath;
        VendorId = slaveStatus.VendorId;
        ProductCode = slaveStatus.ProductCode;
        FaultText = slaveStatus.FaultText;
        IsOnline = slaveStatus.IsOnline;
        HasAlarm = slaveStatus.HasAlarm;
    }

    public int SlaveNo { get; }
    public string Name { get; }
    public string State { get; }
    public string ModuleType { get; }
    public string ModuleState { get; }
    public string TopologyPath { get; }
    public string VendorId { get; }
    public string ProductCode { get; }
    public string FaultText { get; }
    public bool IsOnline { get; }
    public bool HasAlarm { get; }

    public bool Equals(EtherCatSlaveViewModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return SlaveNo == other.SlaveNo
            && Name == other.Name
            && State == other.State
            && ModuleType == other.ModuleType
            && ModuleState == other.ModuleState
            && TopologyPath == other.TopologyPath
            && VendorId == other.VendorId
            && ProductCode == other.ProductCode
            && FaultText == other.FaultText
            && IsOnline == other.IsOnline
            && HasAlarm == other.HasAlarm;
    }

    public override bool Equals(object? obj) => Equals(obj as EtherCatSlaveViewModel);

    public override int GetHashCode()
    {
        return HashCode.Combine(
            HashCode.Combine(SlaveNo, Name, State, ModuleType, ModuleState, TopologyPath),
            HashCode.Combine(VendorId, ProductCode, FaultText, IsOnline, HasAlarm));
    }
}
