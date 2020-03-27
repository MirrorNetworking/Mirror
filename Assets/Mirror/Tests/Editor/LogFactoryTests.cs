using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{

    public class LogFactoryTests
    {
        // A Test behaves as an ordinary method
        [Test]
        public void SameClassSameLogger()
        {
            ILogger logger1 = LogFactory.GetLogger<LogFactoryTests>();
            ILogger logger2 = LogFactory.GetLogger<LogFactoryTests>();
            Assert.That(logger1, Is.SameAs(logger2));
        }

        [Test]
        public void DifferentClassDifferentLogger()
        {
            ILogger logger1 = LogFactory.GetLogger<LogFactoryTests>();
            ILogger logger2 = LogFactory.GetLogger<NetworkManager>();
            Assert.That(logger1, Is.Not.SameAs(logger2));
        }

        [Test]
        public void LogDebugIgnore()
        {
            ILogger logger = LogFactory.GetLogger<LogFactoryTests>();
            logger.filterLogType = LogType.Warning;

            logger.Log("This message should not be logged");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LogWarnOnly()
        {
            ILogger logger = LogFactory.GetLogger<LogFactoryTests>();
            logger.filterLogType = LogType.Warning;

            LogAssert.Expect(LogType.Warning, "LogFactoryTests: This message should be logged");
            logger.LogWarning(nameof(LogFactoryTests), "This message should be logged");
            LogAssert.NoUnexpectedReceived();
        }

    }
}
