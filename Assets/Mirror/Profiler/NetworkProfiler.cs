using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Mirror.Profiler
{

    public class NetworkProfiler
    {
        internal List<NetworkProfileTick> ticks;

        public int MaxFrames { get; set; }
        public int MaxTicks { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="maxFrames"> How many frames should the profiler save</param>
        public NetworkProfiler(int maxFrames = 600)
        {
            ticks = new List<NetworkProfileTick>(maxFrames);
            MaxFrames = maxFrames;
        }

        bool isRecording;

        public IList<NetworkProfileTick> Ticks
        {
            get => ticks;
        }

        public bool IsRecording
        {
            get => isRecording;
            set
            {
                if (value && !isRecording)
                {
                    NetworkDiagnostics.InMessageEvent += OnInMessage;
                    NetworkDiagnostics.OutMessageEvent += OnOutMessage;
                }
                else if (!value && isRecording)
                {
                    NetworkDiagnostics.InMessageEvent -= OnInMessage;
                    NetworkDiagnostics.OutMessageEvent -= OnOutMessage;
                }
                isRecording = value;
            }
        }


        private void OnInMessage(NetworkDiagnostics.MessageInfo messageInfo)
        {
            AddMessage(NetworkDirection.Incoming, messageInfo);

        }

        private void OnOutMessage(NetworkDiagnostics.MessageInfo messageInfo)
        {
            AddMessage(NetworkDirection.Outgoing, messageInfo);
        }


        private void AddMessage(NetworkDirection direction, NetworkDiagnostics.MessageInfo messageInfo)
        {
            NetworkProfileMessage profilerMessage = new NetworkProfileMessage
            {
                Direction = direction,
                Type = messageInfo.message.GetType().Name,
                Name = GetMethodName(messageInfo.message),
                Channel = messageInfo.channel,
                Size = messageInfo.bytes,
                Count = messageInfo.count,
                GameObject = GetGameObject(messageInfo.message)
            };

            // add to the tick
            NetworkProfileTick tick = AddCurrentTick();
            tick.RecordMessage(profilerMessage);
            ticks[ticks.Count - 1] = tick;
            DropOldTicks();
        }

        private NetworkProfileTick AddCurrentTick()
        {
            NetworkProfileTick lastTick = ticks.Count > 0 ? ticks[ticks.Count - 1] : new NetworkProfileTick();

            if (ticks.Count == 0 || lastTick.frameCount != Time.frameCount)
            {
                NetworkProfileTick newTick = new NetworkProfileTick
                {
                    frameCount = Time.frameCount,
                    time = Time.time,
                };

                ticks.Add(newTick);
                return newTick;
            }

            return lastTick;
        }

        public NetworkProfileTick CurrentTick()
        {
            NetworkProfileTick lastTick = ticks.Count > 0 ? ticks[ticks.Count - 1] : new NetworkProfileTick();

            if (ticks.Count == 0 || lastTick.frameCount < Time.frameCount)
            {
                NetworkProfileTick newTick = new NetworkProfileTick
                {
                    frameCount = Time.frameCount
                };

                return newTick;
            }

            return lastTick;
        }

        private void DropOldTicks()
        {
            while (ticks.Count > 0)
            {
                if (ticks[0].frameCount < Time.frameCount - MaxFrames)
                    ticks.RemoveAt(0);
                else
                    break;
            }
            while (ticks.Count > MaxTicks)
            {
                ticks.RemoveAt(0);
            }
        }

        private string GetMethodName(IMessageBase message)
        {
            switch (message)
            {
                case CommandMessage msg:
                    return GetMethodName(msg.functionHash, "InvokeCmd");
                case RpcMessage msg:
                    return GetMethodName(msg.functionHash, "InvokeRpc");
                case SyncEventMessage msg:
                    return GetMethodName(msg.functionHash, "InvokeSyncEvent");
            }
            return null;
        }

        private string GetMethodName(int functionHash, string prefix)
        {
            string fullMethodName = NetworkBehaviour.GetRpcHandler(functionHash).Method.Name;
            if (fullMethodName.StartsWith(prefix, StringComparison.Ordinal))
                return fullMethodName.Substring(prefix.Length);

            return fullMethodName;
        }

        private GameObject GetGameObject(IMessageBase message)
        {
            uint netId = 0;

            switch (message)
            {
                case CommandMessage msg:
                    netId = msg.netId;
                    break;
                case UpdateVarsMessage msg:
                    netId = msg.netId;
                    break;
                case RpcMessage msg:
                    netId = msg.netId;
                    break;
                case SyncEventMessage msg:
                    netId = msg.netId;
                    break;
                case ObjectDestroyMessage msg:
                    netId = msg.netId;
                    break;
                case SpawnMessage msg:
                    return msg.sceneId != 0 ? GetSceneObject(msg.sceneId) : GetPrefab(msg.assetId);
                default:
                    return null;
            }

            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity id))
            {
                return id.gameObject;
            }
            return null;
        }

        private GameObject GetSceneObject(ulong sceneId)
        {
            try
            {
                NetworkIdentity[] ids = Resources.FindObjectsOfTypeAll<NetworkIdentity>();

                foreach (var id in ids)
                {
                    if (id.sceneId == sceneId)
                        return id.gameObject;
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private GameObject GetPrefab(Guid assetId)
        {
            var networkManager = NetworkManager.singleton;

            if (networkManager == null)
                return null;

            GameObject playerPrefab = networkManager.playerPrefab;
            if (playerPrefab != null)
            {
                NetworkIdentity id = playerPrefab.GetComponent<NetworkIdentity>();
                if (id != null && id.assetId == assetId)
                {
                    return playerPrefab;
                }
            }

            foreach (var prefab in networkManager.spawnPrefabs)
            {
                NetworkIdentity id = prefab.GetComponent<NetworkIdentity>();
                if (id != null && id.assetId == assetId)
                {
                    return prefab;
                }
            }
            return null;
        }

        /// <summary>
        /// Saves the current tick array to the specified file relative to the executing assembly
        /// </summary>
        /// <param name="filename">The filename to save to</param>
        public void Save(string filename)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                formatter.Serialize(stream, this.Ticks);
                File.WriteAllBytes(filename, stream.GetBuffer());
            }
        }

        /// <summary>
        /// Loads the ticks from the specified filename
        /// </summary>
        /// <param name="filename">The filename of the capture to load</param>
        public void Load(string filename)
        {
            FileStream stream = File.OpenRead(filename);
            var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            this.ticks = (List<NetworkProfileTick>)formatter.Deserialize(stream);
        }

        /// <summary>
        /// Clears out all the ticks
        /// </summary>
        public void Clear()
        {
            this.ticks.Clear();
        }

        internal NetworkProfileTick GetTick(int frame)
        {
            var tick = Ticks.FirstOrDefault(t => t.frameCount == frame);

            if (tick.frameCount != frame)
                tick.frameCount = frame;

            return tick;
        }

        public NetworkProfileTick GetNextMessageTick(int frame)
        {
            var tick = Ticks.FirstOrDefault(t => t.frameCount > frame);
            if (tick.frameCount == 0)
                tick.frameCount = frame + 1;
            return tick;
        }

        public NetworkProfileTick GetPrevMessageTick(int frame)
        {
            var tick = Ticks.LastOrDefault(t => t.frameCount < frame);
            if (tick.frameCount == 0)
                tick.frameCount = frame - 1;
            return tick;
        }
    }
}
