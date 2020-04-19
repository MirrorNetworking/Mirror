using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Base transport class,  any transport should implement this class
    /// and it's abstract methods
    /// </summary>
    public abstract class AsyncTransport : MonoBehaviour
    {
        public abstract string Scheme { get; }

        /// <summary>
        /// Open up the port and listen for connections
        /// Use in servers.
        /// </summary>
        /// <exception>If we cannot start the transport</exception>
        /// <returns></returns>
        public abstract Task ListenAsync();

        /// <summary>
        /// Stop listening to the port
        /// </summary>
        public abstract void Disconnect();

        /// <summary>
        /// Connect to a server located at a provided uri
        /// </summary>
        /// <param name="uri">address of the server to connect to</param>
        /// <returns>The connection to the server</returns>
        /// <exception>If connection cannot be established</exception>
        public abstract Task<IConnection> ConnectAsync(Uri uri);

        /// <summary>
        /// Accepts a connection from a client. 
        /// After ListenAsync completes,  clients will queue up until you call AcceptAsync
        /// then you get the connection to the client
        /// </summary>
        /// <returns>The connection to a client</returns>
        public abstract Task<IConnection> AcceptAsync();

        /// <summary>
        /// Retrieves the address of this server.
        /// Useful for network discovery
        /// </summary>
        /// <returns>the url at which this server can be reached</returns>
        public abstract Uri ServerUri();
    }
}
