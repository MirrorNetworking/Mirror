// threaded Debug.Log support (mischa 2022)
//
// Editor shows Debug.Logs from different threads.
// Builds don't show Debug.Logs from different threads.
//
// need to hook into logMessageReceivedThreaded to receive them in builds too.
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace Mirror
{
    public static class ThreadLog
    {
        // queue log messages from threads
        struct LogEntry
        {
            public int     threadId;
            public LogType type;
            public string  message;
            public string  stackTrace;

            public LogEntry(int threadId, LogType type, string message, string stackTrace)
            {
                this.threadId = threadId;
                this.type = type;
                this.message = message;
                this.stackTrace = stackTrace;
            }
        }

        // ConcurrentQueue allocations are fine here.
        // logs allocate anywway.
        static readonly ConcurrentQueue<LogEntry> logs =
            new ConcurrentQueue<LogEntry>();

        // main thread id
        static int mainThreadId;

#if !UNITY_EDITOR
        // Editor as of Unity 2021 does log threaded messages.
        // only builds don't.
        // do nothing in editor, otherwise we would log twice.
        // before scene load ensures thread logs are all caught.
        // otherwise some component's Awake may be called before we hooked it up.
        // for example, ThreadedTransport's early logs wouldn't be caught.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {

            // set main thread id
            mainThreadId = Thread.CurrentThread.ManagedThreadId;

            // receive threaded log calls
            Application.logMessageReceivedThreaded -= OnLog; // remove old first. TODO unnecessary?
            Application.logMessageReceivedThreaded += OnLog;

            // process logs on main thread Update
            NetworkLoop.OnLateUpdate -= OnLateUpdate; // remove old first. TODO unnecessary?
            NetworkLoop.OnLateUpdate += OnLateUpdate;

            // log for debugging
            Debug.Log("ThreadLog initialized.");
        }
#endif

        static bool IsMainThread() =>
            Thread.CurrentThread.ManagedThreadId == mainThreadId;

        // callback runs on the same thread where the Debug.Log is called.
        // we can use this to buffer messages for main thread here.
        static void OnLog(string message, string stackTrace, LogType type)
        {
            // only enqueue messages from other threads.
            // otherwise OnLateUpdate main thread logging would be enqueued
            // as well, causing deadlock.
            if (IsMainThread()) return;

            // queue for logging from main thread later
            logs.Enqueue(new LogEntry(Thread.CurrentThread.ManagedThreadId, type, message, stackTrace));
        }

        static void OnLateUpdate()
        {
            // process queued logs on main thread
            while (logs.TryDequeue(out LogEntry entry))
            {
                switch (entry.type)
                {
                    // add [Thread#] prefix to make it super obvious where this log message comes from.
                    // some projects may see unexpected messages that were previously hidden,
                    // since Unity wouldn't log them without ThreadLog.cs.
                    case LogType.Log:
                        Debug.Log($"[Thread{entry.threadId}] {entry.message}\n{entry.stackTrace}");
                        break;
                    case LogType.Warning:
                        Debug.LogWarning($"[Thread{entry.threadId}] {entry.message}\n{entry.stackTrace}");
                        break;
                    case LogType.Error:
                        Debug.LogError($"[Thread{entry.threadId}] {entry.message}\n{entry.stackTrace}");
                        break;
                    case LogType.Exception:
                        Debug.LogError($"[Thread{entry.threadId}] {entry.message}\n{entry.stackTrace}");
                        break;
                    case LogType.Assert:
                        Debug.LogAssertion($"[Thread{entry.threadId}] {entry.message}\n{entry.stackTrace}");
                        break;
                }
            }
        }
    }
}
