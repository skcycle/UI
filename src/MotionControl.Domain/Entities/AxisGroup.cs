namespace MotionControl.Domain.Entities;

public sealed class AxisGroup
{
    public AxisGroup(string groupId, string name, IReadOnlyCollection<Axis> axes)
    {
        GroupId = groupId;
        Name = name;
        Axes = axes;
    }

    public string GroupId { get; }
    public string Name { get; }
    public IReadOnlyCollection<Axis> Axes { get; }
    public Axis? MasterAxis { get; private set; }

    public void SetMasterAxis(Axis masterAxis) => MasterAxis = masterAxis;
}
