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
        protected byte lastSerializedBaselineTick = 0;
        protected byte lastDeserializedBaselineTick = 0;

        [Tooltip("Enable to send all unreliable messages twice. Only useful for extremely fast-paced games since it doubles bandwidth costs.")]
        public bool unreliableRedundancy = false;

        [Tooltip("When sending a reliable baseline, should we also send an unreliable delta or rely on the reliable baseline to arrive in a similar time?")]
        public bool baselineIsDelta = true;

        public virtual void Reset()
        {
            lastSerializedBaselineTick = 0;
            lastDeserializedBaselineTick = 0;
        }

        // user callbacks //////////////////////////////////////////////////////
        // write all baseline sync data in here. this is sent over reliable.
        // TODO reuse in OnSerialize?
        // TODO reuse for ClientToServer?
        protected abstract void OnSerializeServerBaseline(NetworkWriter writer);
        protected abstract void OnSerializeServerDelta(NetworkWriter writer);
        protected abstract void OnSerializeClientBaseline(NetworkWriter writer);
        protected abstract void OnSerializeClientDelta(NetworkWriter writer);

        // TODO move some of this Rpc's code into the base class here for convenience
        //[ClientRpc(channel = Channels.Reliable)] <- define this when inheriting!
        protected abstract void RpcServerToClientBaseline(ArraySegment<byte> data);

        //[ClientRpc(channel = Channels.Unreliable)] <- define this when inheriting!
        protected abstract void RpcServerToClientDelta(ArraySegment<byte> data);

        //[Command(channel = Channels.Reliable)] <- define this when inheriting!
        protected abstract void CmdClientToServerBaseline(ArraySegment<byte> data);

        //[Command(channel = Channels.Unreliable)] <- define this when inheriting!
        protected abstract void CmdClientToServerDelta(ArraySegment<byte> data);

        // this can be used for change detection
        protected virtual bool ShouldSyncServerBaseline(double localTime) => true;
        protected virtual bool ShouldSyncServerDelta(double localTime) => true;
        protected virtual bool ShouldSyncClientBaseline(double localTime) => true;
        protected virtual bool ShouldSyncClientDelta(double localTime) => true;

        // update server ///////////////////////////////////////////////////////
        protected virtual void UpdateServerBaseline(double localTime)
        {
            // send a reliable baseline every 1 Hz
            if (localTime < lastBaselineTime + baselineInterval) return;

            // user check for change detection etc.
            if (!ShouldSyncServerBaseline(localTime)) return;

            // save bandwidth by only transmitting what is needed.
            // -> ArraySegment with random data is slower since byte[] copying
            // -> Vector3? and Quaternion? nullables takes more bandwidth
            byte frameCount = (byte)Time.frameCount; // perf: only access Time.frameCount once!

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // always include baseline tick
                writer.WriteByte(frameCount);
                // include user serialization
                OnSerializeServerBaseline(writer);

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

            // user check for change detection etc.
            if (!ShouldSyncServerDelta(localTime)) return;

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // include baseline tick that this delta is meant for
                writer.WriteByte(lastSerializedBaselineTick);
                // include user serialization
                OnSerializeServerDelta(writer);

                // send (with optional redundancy to make up for message drops)
                RpcServerToClientDelta(writer);
                if (unreliableRedundancy)
                    RpcServerToClientDelta(writer);
            }

            lastDeltaTime = localTime;
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

            // user check for change detection etc.
            if (!ShouldSyncClientBaseline(localTime)) return;

            // save bandwidth by only transmitting what is needed.
            // -> ArraySegment with random data is slower since byte[] copying
            // -> Vector3? and Quaternion? nullables takes more bandwidth
            byte frameCount = (byte)Time.frameCount; // perf: only access Time.frameCount once!

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // always include baseline tick
                writer.WriteByte(frameCount);
                // include user serialization
                OnSerializeClientBaseline(writer);

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

            // user check for change detection etc.
            if (!ShouldSyncClientDelta(localTime)) return;

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // include baseline tick that this delta is meant for
                writer.WriteByte(lastSerializedBaselineTick);
                // include user serialization
                OnSerializeClientDelta(writer);

                // send (with optional redundancy to make up for message drops)
                CmdClientToServerDelta(writer);
                if (unreliableRedundancy)
                    CmdClientToServerDelta(writer);
            }

            lastDeltaTime = localTime;
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
