using NUnit.Framework;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class WorkerThreadTests
    {
        // if using a thread, add them to this list.
        // teardown stop it automatically.
        WorkerThread thread;

        [TearDown]
        public void TearDown()
        {
            // stop thread gracefully with timeout
            if (thread != null)
            {
                if (!thread.StopBlocking(1))
                    Debug.LogWarning("Teardown had to kill worker thread. That's expected for deadlock tests.");
            }
            thread = null;
        }

        [Test]
        public void Callbacks()
        {
            // exceptions in threads are silent by default.
            // guarantee we try/catch them.
            // volatile bool called;
            int initCalled = 0;
            int tickCalled = 0;
            int cleanupCalled = 0;
            void Init()    => Interlocked.Increment(ref initCalled);
            void Tick()    => Interlocked.Increment(ref tickCalled);
            void Cleanup() => Interlocked.Increment(ref cleanupCalled);

            // ctor runs thread and calls callback immediately
            thread         = new WorkerThread("WorkerThreadTests");
            thread.Init    = Init;
            thread.Tick    = Tick;
            thread.Cleanup = Cleanup;
            thread.Start();

            Thread.Sleep(10);
            Assert.That(initCalled, Is.EqualTo(1));
            Assert.That(tickCalled, Is.GreaterThanOrEqualTo(1));
            Assert.That(cleanupCalled, Is.EqualTo(0));

            thread.StopBlocking(1);
            Assert.That(cleanupCalled, Is.EqualTo(1));
        }

        [Test]
        public void ExceptionInInit()
        {
            // exceptions in threads are silent by default.
            // guarantee we try/catch them.
            int cleanupCalled = 0;
            void Init()    => throw new Exception("Test Exception");
            void Cleanup() => Interlocked.Increment(ref cleanupCalled);

            // ctor runs thread and calls callback immediately
            LogAssert.Expect(LogType.Exception, new Regex(".*Test Exception.*"));
            thread         = new WorkerThread("WorkerThreadTests");
            thread.Init    = Init;
            thread.Cleanup = Cleanup;
            thread.Start();

            // ensure active was reset and cleanup is called
            // exception is thrown up the call stack, so this is easy to miss.
            Thread.Sleep(10);
            Assert.That(thread.IsAlive, Is.False);
        }

        [Test]
        public void ExceptionInTick()
        {
            // exceptions in threads are silent by default.
            // guarantee we try/catch them.
            int cleanupCalled = 0;
            void Tick()    => throw new Exception("Test Exception");
            void Cleanup() => Interlocked.Increment(ref cleanupCalled);

            // ctor runs thread and calls callback immediately
            LogAssert.Expect(LogType.Exception, new Regex(".*Test Exception.*"));
            thread         = new WorkerThread("WorkerThreadTests");
            thread.Tick    = Tick;
            thread.Cleanup = Cleanup;
            thread.Start();

            // ensure active was reset and cleanup is called
            // exception is thrown up the call stack, so this is easy to miss.
            Thread.Sleep(10);
            Assert.That(thread.IsAlive, Is.False);
        }

        [Test]
        public void IsAlive()
        {
            thread = new WorkerThread("WorkerThreadTests");
            Assert.False(thread.IsAlive);
            thread.Start();

            Thread.Sleep(10);
            Assert.True(thread.IsAlive);
            thread.StopBlocking(1);
            Assert.False(thread.IsAlive);
        }

        // make sure stop returns immediately, but does stop it eventually
        [Test]
        public void SignalStop()
        {
            thread = new WorkerThread("WorkerThreadTests");
            Assert.False(thread.IsAlive);
            thread.Tick = () => Thread.Sleep(50);
            thread.Start();

            // stop should return immediately, while thread is shutting down
            thread.SignalStop();
            Assert.True(thread.IsAlive);

            // eventually it should have shut down
            Thread.Sleep(70);
            Assert.False(thread.IsAlive);
        }

        // make sure stop returns immediately, but does stop it eventually
        [Test]
        public void StopBlocking()
        {
            thread = new WorkerThread("WorkerThreadTests");
            Assert.False(thread.IsAlive);
            thread.Tick = () => Thread.Sleep(50);
            thread.Start();

            // stop should wait until fully stopped
            Assert.True(thread.StopBlocking(1));
            Assert.False(thread.IsAlive);
        }

        // make sure stop returns immediately, but does stop it eventually
        [Test]
        public void StopBlocking_Deadlocked()
        {
            thread = new WorkerThread("WorkerThreadTests");
            Assert.That(thread.IsAlive, Is.False);
            thread.Tick = () => Thread.Sleep(5000);
            thread.Start();

            // wait for it to start
            Thread.Sleep(50);
            Assert.That(thread.IsAlive, Is.True);

            // stop should detect the deadlock after 1s
            Assert.False(thread.StopBlocking(1));
            Assert.True(thread.IsAlive);
        }
    }
}
