namespace MotionControl.Domain.Entities;

public sealed class WorkHeadPosition
{
    public WorkHeadPosition(string name, string description, double x, double y, double z, double r)
    {
        Name = name;
        Description = description;
        X = x;
        Y = y;
        Z = z;
        R = r;
    }

    public string Name { get; set; }
    public string Description { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double R { get; set; }
}
