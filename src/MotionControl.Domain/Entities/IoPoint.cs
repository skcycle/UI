namespace MotionControl.Domain.Entities;

public sealed class IoPoint
{
    private readonly object _stateLock = new();

    public IoPoint(string name, int address, bool isOutput, string description = "")
    {
        _name = name;
        _address = address;
        IsOutput = isOutput;
        _description = description;
    }

    private string _name;
    public string Name
    {
        get { lock (_stateLock) return _name; }
        private set { lock (_stateLock) _name = value; }
    }

    private int _address;
    public int Address
    {
        get { lock (_stateLock) return _address; }
        private set { lock (_stateLock) _address = value; }
    }

    public bool IsOutput { get; }

    private string _description;
    public string Description
    {
        get { lock (_stateLock) return _description; }
        private set { lock (_stateLock) _description = value; }
    }

    private bool _value;
    public bool Value
    {
        get { lock (_stateLock) return _value; }
        private set { lock (_stateLock) _value = value; }
    }

    /// <summary>轮询线程更新 IO 值（线程安全）。</summary>
    public void Update(bool value)
    {
        lock (_stateLock) { _value = value; }
    }

    public void UpdateMetadata(string name, int address, string description)
    {
        lock (_stateLock)
        {
            _name = name;
            _address = address;
            _description = description;
        }
    }
}
