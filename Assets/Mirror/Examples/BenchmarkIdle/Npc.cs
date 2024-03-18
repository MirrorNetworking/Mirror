// idle object that rarely gets dirty
using UnityEngine;

namespace Mirror.Examples.BenchmarkIdle
{
    public class Npc : NetworkBehaviour
    {
        // component to assign in inspector
        public Renderer rend;

        // the value to set dirty
        [SyncVar] ulong value;

        [Tooltip("Probability that this object just sleeps the whole time without ever getting dirty. (Npcs, Item drops, etc.)")]
        [Range(0, 1)] public float sleepingProbability = 0.80f; // 80% of the objects are sleeping
        bool sleeping;

        [Header("Colors")]
        public Color activeColor = Color.white;
        public Color sleepingColor = Color.red;

        public override void OnStartServer()
        {
            sleeping = Random.value < sleepingProbability;

            // color coding
            // can't do this in update, it's too expensive
            rend.material.color = sleeping ? sleepingColor : activeColor;
        }

        [ServerCallback]
        void Update()
        {
            // set dirty if not sleeping.
            // only counts as dirty every 'syncInterval'.
            if (!sleeping) ++value;
        }
    }
}
