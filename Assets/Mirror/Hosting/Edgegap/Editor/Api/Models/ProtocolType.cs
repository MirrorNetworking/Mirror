namespace Edgegap.Editor.Api.Models
{
    /// <summary>
    /// Unity default: UDP.
    /// (!) UDP !works in WebGL.
    /// </summary>
    public enum ProtocolType
    {
        /// <summary>Unity default - fastest; !works in WebGL.</summary>
        UDP,
        
        /// <summary>Slower, but more reliable; works in WebGL.</summary>
        TCP,

        /// <summary>Slower, but more reliable; works in WebGL.</summary>
        WS,
    }
}
