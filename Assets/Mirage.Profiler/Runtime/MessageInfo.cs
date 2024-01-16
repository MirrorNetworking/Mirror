using Mirror;
using UnityEngine;

namespace Mirage.NetworkProfiler
{
    [System.Serializable]
    public class MessageInfo
    {
        /// <summary>
        /// Order message was sent/received in frame
        /// </summary>
        [SerializeField] private int _order;
        [SerializeField] private int _bytes;
        [SerializeField] private int _count;
        [SerializeField] private string _messageName;
        // unity can't serialize nullable so store as 2 fields
        [SerializeField] private bool _hasNetId;
        [SerializeField] private uint _netId;
        [SerializeField] private string _objectName;
        [SerializeField] private string _rpcName;

        public int Order => _order;
        public string Name => _messageName;
        public int Bytes => _bytes;
        public int Count => _count;
        public int TotalBytes => Bytes * Count;
        public uint? NetId => _hasNetId ? _netId : default;
        public string ObjectName => _objectName;
        public string RpcName => _rpcName;

        public MessageInfo(NetworkDiagnostics.MessageInfo msg, INetworkInfoProvider provider, int order)
        {
            _order = order;
            _bytes = msg.bytes;
            _count = msg.count;
            _messageName = msg.message.GetType().FullName;
            var id = provider.GetNetId(msg);
            _hasNetId = id.HasValue;
            _netId = id.GetValueOrDefault();
            var obj = provider.GetNetworkIdentity(id);
            _objectName = obj != null ? obj.name : null;
            _rpcName = provider.GetRpcName(msg);
        }
    }
}
