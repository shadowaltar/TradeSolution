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

    public long NewLong => Interlocked.Increment(ref _longId);
    
    public int NewInt => Interlocked.Increment(ref _intId);

    public static string NewGuid => System.Guid.NewGuid().ToString();
}
