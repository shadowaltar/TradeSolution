using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Utils;

public class Processes
{
    public static Dictionary<string, Mutex> Owned { get; } = new();
    public static bool IsExistsInSystem(string mutexName)
    {
        if (!Mutex.TryOpenExisting(mutexName, out var existing))
        {
            Owned[mutexName] = new Mutex(true, mutexName);
            return false;
        }
        return true;
    }
}
