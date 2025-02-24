// base class for "Hybrid" sync components.
// inspired by the Quake networking model, but made to scale.
// https://www.jfedor.org/quake3/
using System;
using UnityEngine;

namespace Mirror
{
    public abstract class NetworkBehaviourHybrid : NetworkBehaviour
    {
        // Is this a client with authority over this transform?
        // This component could be on the player object or any object that has been assigned authority to this client.
        protected bool IsClientWithAuthority => isClient && authority;

        [Tooltip("Occasionally send a full reliable state to delta compress against. This only applies to Components with SyncMethod=Unreliable.")]
        public int baselineRate = 1;
        public float baselineInterval => baselineRate < int.MaxValue ? 1f / baselineRate : 0; // for 1 Hz, that's 1000ms
        protected double lastBaselineTime;
        protected double lastDeltaTime;

        // delta compression needs to remember 'last' to compress against.
        byte lastSerializedBaselineTick = 0;
        byte lastDeserializedBaselineTick = 0;

        [Tooltip("Enable to send all unreliable messages twice. Only useful for extremely fast-paced games since it doubles bandwidth costs.")]
        public bool unreliableRedundancy = false;

        [Tooltip("When sending a reliable baseline, should we also send an unreliable delta or rely on the reliable baseline to arrive in a similar time?")]
        public bool baselineIsDelta = true;

        // change detection: we need to do this carefully in order to get it right.
        //
        // DONT just check changes in UpdateBaseline(). this would introduce MrG's grid issue:
        //   server start in A1, reliable baseline sent to client
        //   server moves to A2, unreliabe delta sent to client
        //   server moves to A1, nothing is sent to client becuase last baseline position == position
        //   => client wouldn't know we moved back to A1
        //
        // INSTEAD: every update() check for changes since baseline:
        //   UpdateDelta() keeps sending only if changed since _baseline_
        //   UpdateBaseline() resends if there was any change in the period since last baseline.
        //   => this avoids the A1->A2->A1 grid issue above
        bool changedSinceBaseline = false;

        [Header("Debug")]
        public bool debugLog = false;

        public virtual void ResetState()
        {
            lastSerializedBaselineTick = 0;
            lastDeserializedBaselineTick = 0;
            changedSinceBaseline = false;
        }

        // user callbacks //////////////////////////////////////////////////////
        protected abstract void OnSerializeBaseline(NetworkWriter writer);
        protected abstract void OnDeserializeBaseline(NetworkReader reader, byte baselineTick);

        protected abstract void OnSerializeDelta(NetworkWriter writer);
        protected abstract void OnDeserializeDelta(NetworkReader reader, byte baselineTick);

        // implementations must store the current baseline state when requested:
        // - implementations can use this to compress deltas against
        // - implementations can use this to detect changes since baseline
        // this is called whenever a baseline was sent.
        protected abstract void StoreState();

        // implementations may compare current state to the last stored state.
        // this way we only need to send another reliable baseline if changed since last.
        // this is called every syncInterval, not every baseline sync interval.
        // (see comments where this is called).
        protected abstract bool StateChanged();

        // user callback in case drops due to baseline mismatch need to be logged/visualized/debugged.
        protected virtual void OnDrop(byte lastBaselineTick, byte baselineTick, NetworkReader reader) {}

        // rpcs / cmds /////////////////////////////////////////////////////////
        // reliable baseline.
        // include owner in case of server authority.
        [ClientRpc(channel = Channels.Reliable)]
        void RpcServerToClientBaseline(ArraySegment<byte> data)
        {
            // baseline is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            using (NetworkReaderPooled reader = NetworkReaderPool.Get(data))
            {
                // deserialize
                // save last deserialized baseline tick number to compare deltas against
                lastDeserializedBaselineTick = reader.ReadByte();
                OnDeserializeBaseline(reader, lastDeserializedBaselineTick);
            }
        }

        // unreliable delta.
        // include owner in case of server authority.
        [ClientRpc(channel = Channels.Unreliable)]
        void RpcServerToClientDelta(ArraySegment<byte> data)
        {
            // delta is broadcast to all clients.
            // ignore if this object is owned by this client.
            if (IsClientWithAuthority) return;

            // host mode: baseline Rpc is also sent through host's local connection and applied.
            // applying host's baseline as last deserialized would overwrite the owner client's data and cause jitter.
            // in other words: never apply the rpcs in host mode.
            if (isServer) return;

            // deserialize
            using (NetworkReaderPooled reader = NetworkReaderPool.Get(data))
            {
                // deserialize
                byte baselineTick = reader.ReadByte();

                // ensure this delta is for our last known baseline.
                // we should never apply a delta on top of a wrong baseline.
                if (baselineTick != lastDeserializedBaselineTick)
                {
                    OnDrop(lastDeserializedBaselineTick, baselineTick, reader);

                    // this can happen if unreliable arrives before reliable etc.
                    // no need to log this except when debugging.
                    if (debugLog) Debug.Log($"[{name}] Client: received delta for wrong baseline #{baselineTick}. Last was {lastDeserializedBaselineTick}. Ignoring.");
                    return;
                }

                OnDeserializeDelta(reader, baselineTick);
            }
        }

        [Command(channel = Channels.Reliable)] // reliable baseline
        void CmdClientToServerBaseline(ArraySegment<byte> data)
        {
            // deserialize
            using (NetworkReaderPooled reader = NetworkReaderPool.Get(data))
            {
                // deserialize
                lastDeserializedBaselineTick = reader.ReadByte();
                OnDeserializeBaseline(reader, lastDeserializedBaselineTick);
            }
        }

        [Command(channel = Channels.Unreliable)] // unreliable delta
        void CmdClientToServerDelta(ArraySegment<byte> data)
        {
            using (NetworkReaderPooled reader = NetworkReaderPool.Get(data))
            {
                // deserialize
                byte baselineTick = reader.ReadByte();

                // ensure this delta is for our last known baseline.
                // we should never apply a delta on top of a wrong baseline.
                if (baselineTick != lastDeserializedBaselineTick)
                {
                    OnDrop(lastDeserializedBaselineTick, baselineTick, reader);

                    // this can happen if unreliable arrives before reliable etc.
                    // no need to log this except when debugging.
                    if (debugLog) Debug.Log($"[{name}] Server: received delta for wrong baseline #{baselineTick} from: {connectionToClient}. Last was {lastDeserializedBaselineTick}. Ignoring.");
                    return;
                }

                OnDeserializeDelta(reader, baselineTick);
            }
        }

        // update server ///////////////////////////////////////////////////////
        protected virtual void UpdateServerBaseline(double localTime)
        {
            // send a reliable baseline every 1 Hz
            if (localTime < lastBaselineTime + baselineInterval) return;

            // only sync if changed since last reliable baseline
            if (!changedSinceBaseline) return;

            // save bandwidth by only transmitting what is needed.
            // -> ArraySegment with random data is slower since byte[] copying
            // -> Vector3? and Quaternion? nullables takes more bandwidth
            byte frameCount = (byte)Time.frameCount; // perf: only access Time.frameCount once!

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // serialize
                writer.WriteByte(frameCount);
                OnSerializeBaseline(writer);

                // send (no need for redundancy since baseline is reliable)
                RpcServerToClientBaseline(writer);
            }

            // save the last baseline's tick number.
            // included in baseline to identify which one it was on client
            // included in deltas to ensure they are on top of the correct baseline
            lastSerializedBaselineTick = frameCount;
            lastBaselineTime = NetworkTime.localTime;

            // perf. & bandwidth optimization:
            // send a delta right after baseline to avoid potential head of
            // line blocking, or skip the delta whenever we sent reliable?
            // for example:
            //    1 Hz baseline
            //   10 Hz delta
            //   => 11 Hz total if we still send delta after reliable
            //   => 10 Hz total if we skip delta after reliable
            // in that case, skip next delta by simply resetting last delta sync's time.
            if (baselineIsDelta) lastDeltaTime = localTime;

            // request to store last baseline state (i.e. position) for change detection.
            StoreState();

            // baseline was just sent after a change. reset change detection.
            changedSinceBaseline = false;

            if (debugLog) Debug.Log($"[{name}] Server: sent baseline #{lastSerializedBaselineTick} to: {connectionToClient} at time: {localTime}");
        }

        protected virtual void UpdateServerDelta(double localTime)
        {
            // broadcast to all clients each 'sendInterval'
            // (client with authority will drop the rpc)
            // NetworkTime.localTime for double precision until Unity has it too
            //
            // IMPORTANT:
            // snapshot interpolation requires constant sending.
            // DO NOT only send if position changed. for example:
            // ---
            // * client sends first position at t=0
            // * ... 10s later ...
            // * client moves again, sends second position at t=10
            // ---
            // * server gets first position at t=0
            // * server gets second position at t=10
            // * server moves from first to second within a time of 10s
            //   => would be a super slow move, instead of a wait & move.
            //
            // IMPORTANT:
            // DO NOT send nulls if not changed 'since last send' either. we
            // send unreliable and don't know which 'last send' the other end
            // received successfully.
            //
            // Checks to ensure server only sends snapshots if object is
            // on server authority(!clientAuthority) mode because on client
            // authority mode snapshots are broadcasted right after the authoritative
            // client updates server in the command function(see above), OR,
            // since host does not send anything to update the server, any client
            // authoritative movement done by the host will have to be broadcasted
            // here by checking IsClientWithAuthority.
            // TODO send same time that NetworkServer sends time snapshot?

            if (localTime < lastDeltaTime + syncInterval) return;

            // look for changes every unreliable sendInterval!
            // every reliable interval isn't enough, this would cause MrG's grid issue:
            //   server start in A1, reliable baseline sent to clients
            //   server moves to A2, unreliabe delta sent to clients
            //   server moves back to A1, nothing is sent to clients because last baseline position == position
            //   => clients wouldn't know we moved back to A1
            // every update works, but it's unnecessary overhead since sends only happen every sendInterval
            // every unreliable sendInterval is the perfect place to look for changes.
            if (StateChanged()) changedSinceBaseline = true;

            // only sync on change:
            // unreliable isn't guaranteed to be delivered so this depends on reliable baseline.
            if (!changedSinceBaseline) return;

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // serialize
                writer.WriteByte(lastSerializedBaselineTick);
                OnSerializeDelta(writer);

                // send (with optional redundancy to make up for message drops)
                RpcServerToClientDelta(writer);
                if (unreliableRedundancy)
                    RpcServerToClientDelta(writer);
            }

            lastDeltaTime = localTime;

            if (debugLog) Debug.Log($"[{name}] Server: sent delta for #{lastSerializedBaselineTick} to: {connectionToClient} at time: {localTime}");
        }

        protected virtual void UpdateServerSync()
        {
            // server broadcasts all objects all the time.
            // -> not just ServerToClient: ClientToServer need to be broadcast to others too

            // perf: only grab NetworkTime.localTime property once.
            double localTime = NetworkTime.localTime;

            // broadcast
            UpdateServerBaseline(localTime);
            UpdateServerDelta(localTime);
        }

        // update client ///////////////////////////////////////////////////////
        protected virtual void UpdateClientBaseline(double localTime)
        {
            // send a reliable baseline every 1 Hz
            if (localTime < lastBaselineTime + baselineInterval) return;

            // only sync if changed since last reliable baseline
            if (!changedSinceBaseline) return;

            // save bandwidth by only transmitting what is needed.
            // -> ArraySegment with random data is slower since byte[] copying
            // -> Vector3? and Quaternion? nullables takes more bandwidth
            byte frameCount = (byte)Time.frameCount; // perf: only access Time.frameCount once!

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // serialize
                writer.WriteByte(frameCount);
                OnSerializeBaseline(writer);

                // send (no need for redundancy since baseline is reliable)
                CmdClientToServerBaseline(writer);
            }

            // save the last baseline's tick number.
            // included in baseline to identify which one it was on client
            // included in deltas to ensure they are on top of the correct baseline
            lastSerializedBaselineTick = frameCount;
            lastBaselineTime = NetworkTime.localTime;

            // perf. & bandwidth optimization:
            // send a delta right after baseline to avoid potential head of
            // line blocking, or skip the delta whenever we sent reliable?
            // for example:
            //    1 Hz baseline
            //   10 Hz delta
            //   => 11 Hz total if we still send delta after reliable
            //   => 10 Hz total if we skip delta after reliable
            // in that case, skip next delta by simply resetting last delta sync's time.
            if (baselineIsDelta) lastDeltaTime = localTime;

            // request to store last baseline state (i.e. position) for change detection.
            // IMPORTANT
            // OnSerialize(initial) is called for the spawn payload whenever
            // someone starts observing this object. we always must make
            // this the new baseline, otherwise this happens:
            //   - server broadcasts baseline @ t=1
            //   - server broadcasts delta for baseline @ t=1
            //   - ... time passes ...
            //   - new observer -> OnSerialize sends current position @ t=2
            //   - server broadcasts delta for baseline @ t=1
            //   => client's baseline is t=2 but receives delta for t=1 _!_
            StoreState();

            // baseline was just sent after a change. reset change detection.
            changedSinceBaseline = false;

            if (debugLog) Debug.Log($"[{name}] Client: sent baseline #{lastSerializedBaselineTick} at time: {localTime}");
        }

        protected virtual void UpdateClientDelta(double localTime)
        {
            // send to server each 'sendInterval'
            // NetworkTime.localTime for double precision until Unity has it too
            //
            // IMPORTANT:
            // snapshot interpolation requires constant sending.
            // DO NOT only send if position changed. for example:
            // ---
            // * client sends first position at t=0
            // * ... 10s later ...
            // * client moves again, sends second position at t=10
            // ---
            // * server gets first position at t=0
            // * server gets second position at t=10
            // * server moves from first to second within a time of 10s
            //   => would be a super slow move, instead of a wait & move.
            //
            // IMPORTANT:
            // DO NOT send nulls if not changed 'since last send' either. we
            // send unreliable and don't know which 'last send' the other end
            // received successfully.

            if (localTime < lastDeltaTime + syncInterval) return;

            // look for changes every unreliable sendInterval!
            // every reliable interval isn't enough, this would cause MrG's grid issue:
            //   client start in A1, reliable baseline sent to server and other clients
            //   client moves to A2, unreliabe delta sent to server and other clients
            //   client moves back to A1, nothing is sent to server because last baseline position == position
            //   => server / other clients wouldn't know we moved back to A1
            // every update works, but it's unnecessary overhead since sends only happen every sendInterval
            // every unreliable sendInterval is the perfect place to look for changes.
            if (StateChanged()) changedSinceBaseline = true;

            // only sync on change:
            // unreliable isn't guaranteed to be delivered so this depends on reliable baseline.
            if (!changedSinceBaseline) return;

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // serialize
                writer.WriteByte(lastSerializedBaselineTick);
                OnSerializeDelta(writer);

                // send (with optional redundancy to make up for message drops)
                CmdClientToServerDelta(writer);
                if (unreliableRedundancy)
                    CmdClientToServerDelta(writer);
            }

            lastDeltaTime = localTime;

            if (debugLog) Debug.Log($"[{name}] Client: sent delta for #{lastSerializedBaselineTick} at time: {localTime}");
        }

        protected virtual void UpdateClientSync()
        {
            // client authority, and local player (= allowed to move myself)?
            if (IsClientWithAuthority)
            {
                // https://github.com/vis2k/Mirror/pull/2992/
                if (!NetworkClient.ready) return;

                // perf: only grab NetworkTime.localTime property once.
                double localTime = NetworkTime.localTime;

                UpdateClientBaseline(localTime);
                UpdateClientDelta(localTime);
            }
        }

        // Update() without LateUpdate() split: otherwise perf. is cut in half!
        protected virtual void Update()
        {
            // if server then always sync to others.
            if (isServer) UpdateServerSync();
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient) UpdateClientSync();
        }

        // OnSerialize(initial) is called every time when a player starts observing us.
        // note this is _not_ called just once on spawn.
        // call this from inheriting classes immediately in OnSerialize().
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (initialState)
            {
                // always include the tick for deltas to compare against.
                byte frameCount = (byte)Time.frameCount; // perf: only access Time.frameCount once!
                writer.WriteByte(frameCount);

                // IMPORTANT
                // OnSerialize(initial) is called for the spawn payload whenever
                // someone starts observing this object. we always must make
                // this the new baseline, otherwise this happens:
                //   - server broadcasts baseline @ t=1
                //   - server broadcasts delta for baseline @ t=1
                //   - ... time passes ...
                //   - new observer -> OnSerialize sends current position @ t=2
                //   - server broadcasts delta for baseline @ t=1
                //   => client's baseline is t=2 but receives delta for t=1 _!_
                lastSerializedBaselineTick = (byte)Time.frameCount;
                lastBaselineTime = NetworkTime.localTime;

                // request to store last baseline state (i.e. position) for change detection.
                StoreState();
            }
        }

        // call this from inheriting classes immediately in OnDeserialize().
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                // save last deserialized baseline tick number to compare deltas against
                lastDeserializedBaselineTick = reader.ReadByte();
            }
        }
    }
}
