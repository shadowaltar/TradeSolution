using Common;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using TradeCommon.Runtime;

namespace TradeCommon.Database;
public class Persistence : IDisposable
{
    private readonly IStorage _storage;
    private readonly ConcurrentQueue<PersistenceTask> _tasks = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Pool<PersistenceTask> _taskPool = new();

    private int _currentThreadId;
    private bool _isEmpty = true;
    private bool _isRunning;

    public bool IsEmpty => _isEmpty;

    public Persistence(IStorage storage)
    {
        Task.Factory.StartNew(async (t) =>
        {
            _currentThreadId = Thread.CurrentThread.ManagedThreadId;

            _isRunning = true;
            await Run();
        }, TaskCreationOptions.LongRunning, CancellationToken.None);
        _storage = storage;
    }

    public int Insert<T>(T entry, string? tableNameOverride = null, bool isUpsert = true, bool isSynchronous = false, [CallerMemberName] string callerInfo = "")
    {
        if (entry == null)
            return 0;

        if (entry is SecurityRelatedEntry sre)
        {
            Assertion.ShallNever(sre.Security == null);
        }

        var task = _taskPool.Lease();
        task.SetEntry(entry);
        task.Action = isUpsert ? DatabaseActionType.Upsert : DatabaseActionType.Insert;
        task.Type = typeof(T);
        task.TableNameOverride = tableNameOverride;
        task.CallerInfo = callerInfo;
        if (!isSynchronous)
            _tasks.Enqueue(task);
        else
            return AsyncHelper.RunSync(() => RunTask(task));
        return int.MinValue;
    }

    public int Insert<T>(List<T> entries, string? tableNameOverride = null, bool isUpsert = true, bool isSynchronous = false, [CallerMemberName] string callerInfo = "")
    {
        if (entries.IsNullOrEmpty())
            return 0;

        foreach (var entry in entries)
        {
            if (entry is SecurityRelatedEntry sre)
            {
                Assertion.ShallNever(sre.Security == null);
            }
        }

        var task = _taskPool.Lease();
        task.SetEntries(entries);
        task.Action = isUpsert ? DatabaseActionType.Upsert : DatabaseActionType.Insert;
        task.Type = typeof(T);
        task.TableNameOverride = tableNameOverride;
        task.CallerInfo = callerInfo;
        if (!isSynchronous)
            _tasks.Enqueue(task);
        else
            return AsyncHelper.RunSync(() => RunTask(task));
        return int.MinValue;
    }

    public PersistenceTask? Delete<T>(T entry, string? tableNameOverride = null)
    {
        if (entry == null)
            return null;

        if (entry is SecurityRelatedEntry sre)
        {
            Assertion.ShallNever(sre.Security == null);
        }

        var task = _taskPool.Lease();
        task.SetEntry(entry);
        task.Action = DatabaseActionType.Delete;
        task.Type = typeof(T);
        task.TableNameOverride = tableNameOverride;
        _tasks.Enqueue(task);
        return task;
    }
    
    /// <summary>
    /// Block until all queued database actions are finished.
    /// </summary>
    public void WaitAll()
    {
        if (Environment.CurrentManagedThreadId == _currentThreadId)
            throw Exceptions.Invalid("Must not call method to wait for all tasks to finish inside the Persistence instance.");
        Threads.WaitUntil(() => _isEmpty);
    }

    private async Task Run()
    {
        while (_isRunning)
        {
            if (_tasks.TryDequeue(out var task))
            {
                _isEmpty = false;
                _ = await RunTask(task);
            }
            else
            {
                _isEmpty = true;
                Thread.Sleep(100);
            }
        }
    }

    private async Task<int> RunTask(PersistenceTask task)
    {
        var count = 0;
        if (task.Action is DatabaseActionType.Insert or DatabaseActionType.Upsert)
            count = await _storage.Insert(task);
        else if (task.Action is DatabaseActionType.Delete)
            count = await _storage.Delete(task);
        task.Clear();
        _taskPool.Return(task);
        return count;
    }

    public void Dispose()
    {
        _isRunning = false;
        _cancellationTokenSource.Cancel();
        _taskPool.Dispose();
        _tasks.Clear();
    }
}
