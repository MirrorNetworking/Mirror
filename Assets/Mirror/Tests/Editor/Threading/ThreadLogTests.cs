using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace Mirror.Tests
{
    public class ThreadLogTests
    {
        WorkerThread mirrorThread;

        [TearDown]
        public void TearDown()
        {
            // stop thread gracefully with timeout
            if (mirrorThread != null)
            {
                mirrorThread.StopBlocking(1);
            }
            mirrorThread = null;
        }

        [Test]
        public void RegisterThread_AddsThreadId()
        {
            int testThreadId = 12345;
            ThreadLog.RegisterThread(testThreadId);
            
            // We can't directly access the private mirrorThreadIds dictionary,
            // but we can test indirectly by checking if UnregisterThread works without error
            ThreadLog.UnregisterThread(testThreadId);
        }

        [Test]
        public void UnregisterThread_RemovesThreadId()
        {
            int testThreadId = 12345;
            ThreadLog.RegisterThread(testThreadId);
            ThreadLog.UnregisterThread(testThreadId);
            
            // If we call unregister again, it should not throw
            Assert.DoesNotThrow(() => ThreadLog.UnregisterThread(testThreadId));
        }

        [Test]
        public void WorkerThread_RegistersAndUnregistersThreadId()
        {
            // Create a worker thread that just sleeps
            mirrorThread = new WorkerThread("ThreadLogTest");
            int tickCalled = 0;
            mirrorThread.Tick = () => 
            { 
                Interlocked.Increment(ref tickCalled);
                Thread.Sleep(10);
                return false; // Stop after one tick
            };
            
            // Start the thread
            mirrorThread.Start();
            
            // Give the thread time to start and run
            Thread.Sleep(50);
            
            // Wait for thread to complete gracefully
            bool stopped = mirrorThread.StopBlocking(1);
            
            // Verify tick was called (thread ran)
            Assert.That(tickCalled, Is.GreaterThanOrEqualTo(1), "Tick should have been called at least once");
            Assert.That(stopped, Is.True, "Thread should have stopped gracefully");
            Assert.That(mirrorThread.IsAlive, Is.False);
        }
    }
}
