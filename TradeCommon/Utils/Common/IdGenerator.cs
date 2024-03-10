namespace Common;
public class IdGenerator(string name)
{
    private static readonly long _baseTicks = new DateTime(2000, 1, 1).Ticks;

    private long _longId;
    private int _intId;
    private volatile int _sequentialSuffix;
    private volatile int _negativeSequentialSuffix;

    private readonly object _lock = new();

    public string Name { get; } = name;

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
                return ((DateTime.UtcNow.Ticks - _baseTicks) * 10) + _sequentialSuffix;
            }
        }
    }

    public long NewNegativeTimeBasedId
    {
        get
        {
            lock (_lock)
            {
                _negativeSequentialSuffix--;
                if (_negativeSequentialSuffix == 0)
                    _negativeSequentialSuffix = 10;
                return (-(DateTime.UtcNow.Ticks - _baseTicks) * 10) + _sequentialSuffix;
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
    private static readonly Dictionary<Type, IdGenerator> _idGenerators = [];
    
    public static IdGenerator Get()
    {
        return Get<object>();
    }

    public static IdGenerator Get<T>()
    {
        lock (_idGenerators)
        {
            var t = typeof(T);
            return _idGenerators.GetOrCreate(t, () => new IdGenerator(t.Name + "IdGen"));
        }
    }
}
