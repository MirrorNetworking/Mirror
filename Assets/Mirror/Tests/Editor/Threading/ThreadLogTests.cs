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
            bool tickCalled = false;
            mirrorThread.Tick = () => 
            { 
                tickCalled = true;
                Thread.Sleep(10);
                return false; // Stop after one tick
            };
            
            // Start the thread
            mirrorThread.Start();
            
            // Wait for thread to complete
            Thread.Sleep(100);
            
            // Verify tick was called (thread ran)
            Assert.That(tickCalled, Is.True);
            Assert.That(mirrorThread.IsAlive, Is.False);
        }
    }
}
