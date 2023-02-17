using UnityEngine;
using Mirror;
using System;

namespace TestNT
{
    public class NTReliableExt : NetworkTransformReliable
    {
        public Action<Vector3, Vector3> VelRotChangedAction;

        [Header("Snapshot Interpolation")]
        //public double t;
        //public int fromIndex;
        //public int toIndex;
        public Vector3 velocity;

        #region Unity Callbacks

        protected override void OnValidate()
        {
            base.OnValidate();
        }

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        #endregion

        #region NetworkTransformBase Methods

        protected override bool Changed(TransformSnapshot current)
        {
            return base.Changed(current);
        }

        protected override void OnServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            base.OnServerToClientSync(position, rotation, scale);
        }

        protected override void OnClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            base.OnClientToServerSync(position, rotation, scale);
        }

        #endregion

        #region NetworkTransformReliable Methods

        protected override void Apply(TransformSnapshot interpolated, TransformSnapshot endGoal)
        {
            if (!isOwned)
            {
                Vector3 velChange = (transform.position - interpolated.position) / Time.deltaTime;
                Vector3 rotChange = (transform.rotation.eulerAngles - interpolated.rotation.eulerAngles) / Time.deltaTime;
                velocity = velChange;
                VelRotChangedAction?.Invoke(velChange, rotChange);
            }

            base.Apply(interpolated, endGoal);
        }

        protected override TransformSnapshot Construct()
        {
            return base.Construct();
        }

        public override void Reset()
        {
            base.Reset();
        }

        #endregion
    }
}
