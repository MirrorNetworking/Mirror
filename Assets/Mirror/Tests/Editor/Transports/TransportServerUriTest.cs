using System;
using NUnit.Framework;

namespace Mirror.Tests.Transports
{
    /// <summary>
    /// Unit tests for Transport.TryBuildValidUri helper method.
    /// Tests various edge cases with non-ASCII and invalid URI characters in hostnames.
    /// </summary>
    [TestFixture]
    public class TransportServerUriTest
    {
        // Test helper class to expose the protected TryBuildValidUri method
        private class TestableTransport : Transport
        {
            public static Uri TestTryBuildValidUri(string scheme, string hostname, int port)
            {
                return TryBuildValidUri(scheme, hostname, port);
            }

            public override bool Available() => true;
            public override bool ClientConnected() => false;
            public override void ClientConnect(string address) { }
            public override void ClientSend(ArraySegment<byte> segment, int channelId = 0) { }
            public override void ClientDisconnect() { }
            public override Uri ServerUri() => null;
            public override bool ServerActive() => false;
            public override void ServerStart() { }
            public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = 0) { }
            public override void ServerDisconnect(int connectionId) { }
            public override string ServerGetClientAddress(int connectionId) => "";
            public override void ServerStop() { }
            public override void Shutdown() { }
            public override int GetMaxPacketSize(int channelId = 0) => 1200;
        }

        [Test]
        [TestCase("kcp", 7777)]
        [TestCase("tcp4", 8888)]
        [TestCase("ws", 9999)]
        public void TryBuildValidUri_WithChineseCharacters_ReturnsIdnEncodedUri(string scheme, int port)
        {
            // Chinese characters in hostname (测试PC)
            string hostname = "测试PC";
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should be IDN encoded (punycode)
            Assert.AreEqual("xn--pc-ih2ek29h", result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", 7777)]
        [TestCase("tcp4", 8888)]
        public void TryBuildValidUri_WithJapaneseCharacters_ReturnsIdnEncodedUri(string scheme, int port)
        {
            // Japanese characters in hostname
            string hostname = "日本語";
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should be IDN encoded (punycode)
            Assert.AreEqual("xn--wgv71a119e", result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", 7777)]
        public void TryBuildValidUri_WithAccentedCharacters_ReturnsIdnEncodedUri(string scheme, int port)
        {
            // Accented characters in hostname
            string hostname = "café";
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should be IDN encoded (punycode)
            Assert.AreEqual("xn--caf-dma", result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", 7777)]
        [TestCase("tcp4", 8888)]
        public void TryBuildValidUri_WithSpaceInHostname_FallsBackToLocalhost(string scheme, int port)
        {
            // Invalid character: space
            string hostname = "test PC";
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should fallback to localhost
            Assert.AreEqual("localhost", result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", 7777)]
        public void TryBuildValidUri_WithAngleBrackets_FallsBackToLocalhost(string scheme, int port)
        {
            // Invalid characters: angle brackets
            string hostname = "test<PC>";
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should fallback to localhost
            Assert.AreEqual("localhost", result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", 7777)]
        public void TryBuildValidUri_WithSquareBrackets_FallsBackToLocalhost(string scheme, int port)
        {
            // Invalid characters: square brackets (not valid for IPv6 format)
            string hostname = "test[PC]";
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should fallback to localhost
            Assert.AreEqual("localhost", result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", 7777)]
        public void TryBuildValidUri_WithBackslash_FallsBackToLocalhost(string scheme, int port)
        {
            // Invalid character: backslash
            string hostname = "PC\\test";
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should fallback to localhost
            Assert.AreEqual("localhost", result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", 7777)]
        public void TryBuildValidUri_WithPercentSign_FallsBackToLocalhost(string scheme, int port)
        {
            // Invalid character: percent sign
            string hostname = "test%PC";
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should fallback to localhost
            Assert.AreEqual("localhost", result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", 7777)]
        [TestCase("tcp4", 8888)]
        public void TryBuildValidUri_WithEmptyString_FallsBackToLocalhost(string scheme, int port)
        {
            // Edge case: empty string
            string hostname = "";
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should fallback to localhost
            Assert.AreEqual("localhost", result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", 7777)]
        [TestCase("tcp4", 8888)]
        public void TryBuildValidUri_WithNull_FallsBackToLocalhost(string scheme, int port)
        {
            // Edge case: null hostname
            string hostname = null;
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should fallback to localhost
            Assert.AreEqual("localhost", result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", 7777)]
        [TestCase("tcp4", 8888)]
        public void TryBuildValidUri_WithWhitespace_FallsBackToLocalhost(string scheme, int port)
        {
            // Edge case: whitespace only
            string hostname = "   ";
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should fallback to localhost
            Assert.AreEqual("localhost", result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", "localhost", 7777)]
        [TestCase("tcp4", "my-server", 8888)]
        [TestCase("ws", "example.com", 9999)]
        public void TryBuildValidUri_WithValidAsciiHostname_ReturnsHostnameAsIs(string scheme, string hostname, int port)
        {
            // Normal ASCII hostnames should work as-is
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should use hostname as-is
            Assert.AreEqual(hostname, result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", "192.168.1.1", 7777)]
        [TestCase("tcp4", "10.0.0.1", 8888)]
        public void TryBuildValidUri_WithIpAddress_ReturnsIpAsIs(string scheme, string hostname, int port)
        {
            // IP addresses should work as-is
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should use IP as-is
            Assert.AreEqual(hostname, result.Host);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        [TestCase("kcp", "server-name", 7777)]
        [TestCase("tcp4", "my_server", 8888)]
        public void TryBuildValidUri_WithValidSpecialCharacters_ReturnsHostname(string scheme, string hostname, int port)
        {
            // Valid special characters (hyphen, underscore)
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(scheme, result.Scheme);
            Assert.AreEqual(port, result.Port);
            // Should use hostname (might be modified by Uri normalization)
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }

        [Test]
        public void TryBuildValidUri_AlwaysReturnsValidUri()
        {
            // Test that method NEVER returns null or throws exception
            string[] weirdHostnames = 
            {
                null,
                "",
                "   ",
                "测试PC",
                "日本語",
                "café",
                "test PC",
                "test<PC>",
                "test[PC]",
                "PC\\test",
                "test%PC",
                "test#PC",
                "test@PC",
                "@#$%^&*()",
                "server name with many spaces",
                "\t\n\r",
                "localhost",
                "127.0.0.1"
            };

            foreach (string hostname in weirdHostnames)
            {
                Uri result = TestableTransport.TestTryBuildValidUri("kcp", hostname, 7777);
                
                Assert.IsNotNull(result, $"TryBuildValidUri should never return null for hostname: {hostname}");
                Assert.IsFalse(string.IsNullOrEmpty(result.Host), $"Host should not be empty for hostname: {hostname}");
                Assert.AreEqual("kcp", result.Scheme, $"Scheme should be preserved for hostname: {hostname}");
                Assert.AreEqual(7777, result.Port, $"Port should be preserved for hostname: {hostname}");
            }
        }

        [Test]
        public void TryBuildValidUri_GuaranteesNoEmptyHost()
        {
            // This test specifically addresses the original issue:
            // "Host is empty/invalid; URI becomes `kcp://:port`"
            
            // Test the exact scenario from the issue: Chinese character hostname
            string problematicHostname = "测试PC";
            
            Uri result = TestableTransport.TestTryBuildValidUri("kcp", problematicHostname, 7777);
            
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), 
                "The original issue was that Host becomes empty. This should now be fixed.");
            
            // Verify the URI string doesn't have the format "kcp://:7777"
            string uriString = result.ToString();
            Assert.IsFalse(uriString.Contains("://:"), 
                "URI should not have empty host (format scheme://:port)");
        }

        [Test]
        [TestCase("kcp", 7777)]
        [TestCase("tcp4", 8888)]
        public void TryBuildValidUri_WithMultipleInvalidCharacters_StillReturnsValidUri(string scheme, int port)
        {
            // Hostname with multiple types of invalid characters
            string hostname = "test PC<>[]\\#@%";
            
            Uri result = TestableTransport.TestTryBuildValidUri(scheme, hostname, port);
            
            Assert.IsNotNull(result);
            Assert.AreEqual("localhost", result.Host, 
                "Should fallback to localhost when hostname has multiple invalid characters");
            Assert.IsFalse(string.IsNullOrEmpty(result.Host), "Host should not be empty");
        }
    }
}
