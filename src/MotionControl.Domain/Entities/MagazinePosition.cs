namespace MotionControl.Domain.Entities;

public sealed class MagazinePosition
{
    public MagazinePosition(string name, string description, string kind, double x, double y, double z)
    {
        Name = name;
        Description = description;
        Kind = kind;
        X = x;
        Y = y;
        Z = z;
    }

    public string Name { get; set; }
    public string Description { get; set; }
    public string Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
