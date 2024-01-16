using System.Text.RegularExpressions;
using Mirror;
using Mirror.RemoteCalls;
using UnityEngine;
using UnityEngine.Assertions;

namespace Mirage.NetworkProfiler
{
    /// <summary>
    /// Returns information about NetworkMessage
    /// </summary>
    public interface INetworkInfoProvider
    {
        uint? GetNetId(NetworkDiagnostics.MessageInfo info);
        NetworkIdentity GetNetworkIdentity(uint? netId);
        string GetRpcName(NetworkDiagnostics.MessageInfo info);
    }

    public class NetworkInfoProvider : INetworkInfoProvider
    {
        public uint? GetNetId(NetworkDiagnostics.MessageInfo info)
        {
            switch (info.message)
            {
                case CommandMessage msg: return msg.netId;
                case RpcMessage msg: return msg.netId;
                case SpawnMessage msg: return msg.netId;
                case ChangeOwnerMessage msg: return msg.netId;
                case ObjectDestroyMessage msg: return msg.netId;
                case ObjectHideMessage msg: return msg.netId;
                case EntityStateMessage msg: return msg.netId;
                default: return default;
            }
        }

        public NetworkIdentity GetNetworkIdentity(uint? netId)
        {
            if (!netId.HasValue)
                return null;

            if (NetworkServer.active)
            {
                NetworkServer.spawned.TryGetValue(netId.Value, out var identity);
                return identity;
            }

            if (NetworkClient.active)
            {
                NetworkClient.spawned.TryGetValue(netId.Value, out var identity);
                return identity;
            }

            return null;
        }

        public string GetRpcName(NetworkDiagnostics.MessageInfo info)
        {
            switch (info.message)
            {
                case CommandMessage msg:
                    return GetRpcName(msg.netId, msg.componentIndex, msg.functionHash);
                case RpcMessage msg:
                    return GetRpcName(msg.netId, msg.componentIndex, msg.functionHash);
                default: return string.Empty;
            }
        }

#if MIRROR_2022_9_OR_NEWER
        private string GetRpcName(uint netId, int componentIndex, int functionIndex)
#else
        private string GetRpcName(uint netId, int componentIndex, ushort functionIndex)
#endif
        {
            var hash = functionIndex;
            var remoteCallDelegate = RemoteProcedureCalls.GetDelegate(hash);
            if (remoteCallDelegate != null)
            {
                string fixedMethodName = MirrorMethodNameTrimmer.FixMethodName(remoteCallDelegate.Method.Name);
                string methodFullName = $"{remoteCallDelegate.Method.DeclaringType?.FullName}.{fixedMethodName}";
                return methodFullName;
            }

            // todo some error maybe
            return string.Empty;
        }
        
        
        /// <summary>
        /// Some stuff from Mirror.Weaver
        /// </summary>
        private static class MirrorMethodNameTrimmer
        {
            private static readonly Regex regex1 = new Regex($"^InvokeUserCode_", RegexOptions.Compiled);
            private static readonly Regex regex2 = new Regex("__[A-Z][\\w`]*$", RegexOptions.Compiled);

            public static string FixMethodName(string methodName)
            {
                methodName = regex1.Replace(methodName, "");
                methodName = regex2.Replace(methodName, "");

                return methodName;
            }
        }
    }
}
