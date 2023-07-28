using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common;
public class IdGenerator
{
    private long _longId;
    private int _intId;
    private volatile int _sequentialSuffix;

    private readonly object _lock = new();

    public long NewLong => Interlocked.Increment(ref _longId);

    public int NewInt => Interlocked.Increment(ref _intId);

    public static string NewGuid => System.Guid.NewGuid().ToString();

    public long NewTimeBasedId
    {
        get
        {
            lock (_lock)
            {
                _sequentialSuffix++;
                if (_sequentialSuffix == 100)
                    _sequentialSuffix = 0;
                return DateTime.UtcNow.Ticks * 100 + _sequentialSuffix;
            }
        }
    }
}
