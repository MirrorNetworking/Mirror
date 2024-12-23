// base class for "Hybrid" sync components.
// inspired by the Quake networking model, but made to scale.
// https://www.jfedor.org/quake3/
using UnityEngine;

namespace Mirror
{
    public abstract class NetworkBehaviourHybrid : NetworkBehaviour
    {
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
