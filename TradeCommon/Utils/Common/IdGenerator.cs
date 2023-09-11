namespace Common;
public class IdGenerator
{
    private long _longId;
    private int _intId;
    private volatile int _sequentialSuffix;

    private readonly object _lock = new();

    public IdGenerator(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public long NewLong => Interlocked.Increment(ref _longId);

    public int NewInt => Interlocked.Increment(ref _intId);

    public static string NewGuid => Guid.NewGuid().ToString();

    public long NewTimeBasedId
    {
        get
        {
            lock (_lock)
            {
                _sequentialSuffix++;
                if (_sequentialSuffix == 10)
                    _sequentialSuffix = 0;
                return DateTime.UtcNow.Ticks * 10 + _sequentialSuffix;
            }
        }
    }

    public override string? ToString()
    {
        return Name;
    }
}

public class IdGenerators
{
    private static readonly Dictionary<Type, IdGenerator> _idGenerators = new();
    public static IdGenerator Get<T>()
    {
        lock (_idGenerators)
        {
            var t = typeof(T);
            return _idGenerators.GetOrCreate(t, () => new IdGenerator(t.Name + "IdGen"));
        }
    }
}
