using System;
using UnityEngine;
using Mirror;

public class NTUnreliableExt : NetworkTransform
{
    public Action<Vector3, Vector3> VelRotChangedAction;

    [Header("Snapshot Interpolation")]
    public Vector3 velocity;
    public Vector3 angVelocity;

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
}
