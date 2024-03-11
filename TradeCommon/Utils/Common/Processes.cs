namespace Common;

public class Processes
{
    public static Dictionary<string, Mutex> Owned { get; } = [];
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
