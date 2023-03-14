using UnityEngine;
using Mirror;
using System;

namespace TestNT
{
    public class NTReliableExt : NetworkTransformReliable
    {
        public Action<Vector3, Vector3> VelRotChangedAction;

        [Header("Snapshot Interpolation")]
        public Vector3 velocity;
        public Vector3 angVelocity;

        #region NetworkTransformReliable Methods

        protected override void Apply(TransformSnapshot interpolated, TransformSnapshot endGoal)
        {
            base.Apply(interpolated, endGoal);

            if (!isOwned)
            {
                velocity = (transform.position - interpolated.position) / Time.deltaTime;
                angVelocity = (transform.rotation.eulerAngles - interpolated.rotation.eulerAngles) / Time.deltaTime;
                VelRotChangedAction?.Invoke(velocity, angVelocity);
            }
        }

        #endregion
    }
}
