// worker thread for Unity (mischa 2022)
// thread with proper exception handling, profling, init, cleanup, etc. for Unity.
// use this from main thread.
using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Mirror
{
    public class WorkerThread
    {
        readonly Thread thread;

        protected volatile bool active;

        // stopwatch so we don't need to use Unity's Time (engine independent)
        readonly Stopwatch watch = new Stopwatch();

        // callbacks need to be set after constructor.
        // inheriting classes can't pass their member funcs to base ctor.
        // don't set them while the thread is running!
        // -> Tick() returns a bool so it can easily stop the thread
        //    without needing to throw InterruptExceptions or similar.
        public Action Init;
        public Func<bool> Tick;
        public Action Cleanup;

        public WorkerThread(string identifier)
        {
            // start the thread wrapped in safety guard
            // if main application terminates, this thread needs to terminate too
            thread = new Thread(
                () => Guard(identifier)
            );
            thread.IsBackground = true;
        }

        public void Start()
        {
            // only if thread isn't already running
            if (thread.IsAlive)
            {
                Debug.LogWarning("WorkerThread is still active, can't start it again.");
                return;
            }

            active = true;
            thread.Start();
        }

        // signal the thread to stop gracefully.
        // returns immediately, but the thread may take a while to stop.
        // may be overwritten to clear more flags like 'computing' etc.
        public virtual void SignalStop() => active = false;

        // wait for the thread to fully stop
        public bool StopBlocking(float timeout)
        {
            // only if alive
            if (!thread.IsAlive) return true;

            // double precision for long running servers.
            watch.Restart();

            // signal to stop
            SignalStop();

            // wait while thread is still alive
            while (IsAlive)
            {
                // simply wait..
                Thread.Sleep(0);

                // deadlock detection
                if (watch.Elapsed.TotalSeconds >= timeout)
                {
                    // force kill all threads as last resort to stop them.
                    // return false to indicate deadlock.
                    Interrupt();
                    return false;
                }
            }
            return true;
        }

        public bool IsAlive => thread.IsAlive;

        // signal an interrupt in the thread.
        // this function is very safe to use.
        // https://stackoverflow.com/questions/5950994/thread-abort-vs-thread-interrupt
        //
        // note this does not always kill the thread:
        // "If this thread is not currently blocked in a wait, sleep, or join
        //  state, it will be interrupted when it next begins to block."
        // https://docs.microsoft.com/en-us/dotnet/api/system.threading.thread.interrupt?view=net-6.0
        //
        // in other words, "while (true) {}" wouldn't throw an interrupt exception.
        // and that's _okay_. using interrupt is safe & best practice.
        // => Unity still aborts deadlocked threads on script reload.
        // => and we catch + warn on AbortException.
        public void Interrupt() => thread.Interrupt();

        // thread constructor needs callbacks.
        // always define them, and make them call actions.
        // those can be set at any time.
        void OnInit()    => Init?.Invoke();
        bool OnTick()    => Tick?.Invoke() ?? false;
        void OnCleanup() => Cleanup?.Invoke();

        // guarded wrapper for thread code.
        // catches exceptions which would otherwise be silent.
        // shows in Unity profiler.
        // etc.
        public void Guard(string identifier)
        {
            try
            {
                // log when work begins = thread starts.
                // very important for debugging threads.
                Debug.Log($"{identifier}: started.");

                // show this thread in Unity profiler
                Profiler.BeginThreadProfiling("Mirror Worker Threads", $"{identifier}");

                // run init once
                OnInit();

                // run thread func while active
                while (active)
                {
                    // Tick() returns a bool so it can easily stop the thread
                    // without needing to throw InterruptExceptions or similar.
                    if (!OnTick()) break;
                }
            }
            // Thread.Interrupt() will gracefully raise a InterruptedException.
            catch (ThreadInterruptedException)
            {
                Debug.Log($"{identifier}: interrupted. That's okay.");
            }
            // Unity domain reload will cause a ThreadAbortException.
            // for example, when saving a changed script while in play mode.
            catch (ThreadAbortException)
            {
                Debug.LogWarning($"{identifier}: aborted. This may happen after domain reload. That's okay.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                // run cleanup (if any)
                active = false;
                OnCleanup();

                // remove this thread from Unity profiler
                Profiler.EndThreadProfiling();

                // log when work ends = thread terminates.
                // very important for debugging threads.
                // 'finally' to log no matter what (even if exceptions)
                Debug.Log($"{identifier}: ended.");
            }
        }
    }
}
