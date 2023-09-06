using System.Collections.Concurrent;
using TradeCommon.Database;

namespace TradeDataCore.Database;
//public class Persistence
//{
//    private readonly ConcurrentQueue<IPersistenceTask> _tasks = new();

//    private volatile bool _isRunning;
//    private bool _isPaused;

//    public Action<IPersistenceTask>? Persisted;

//    public Persistence()
//    {
//        Start();
//    }

//    public void Start()
//    {
//        if (_isRunning) return;
//        _isRunning = true;

//        Run();
//    }

//    public void Stop() => _isRunning = false;

//    public void Pause() => _isPaused = true;
    
//    public void Resume() => _isPaused = false;

//    public void Submit(IPersistenceTask task) => _tasks.Enqueue(task);

//    private void Run()
//    {
//        Task.Factory.StartNew(async () =>
//        {
//            _isRunning = true;
//            while (_isRunning)
//            {
//                if (_isPaused)
//                {
//                    Thread.Sleep(50);
//                    continue;
//                }

//                while (_tasks.TryDequeue(out var task))
//                {
//                    await Storage.Insert(task);
//                    Persisted?.Invoke(task);
//                }

//                Thread.Sleep(50);
//            }
//        }, TaskCreationOptions.LongRunning);

//    }
//}
