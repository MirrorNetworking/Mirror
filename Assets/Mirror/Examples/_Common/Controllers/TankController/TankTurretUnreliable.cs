using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("Network/Tank Turret (Unreliable)")]
    [RequireComponent(typeof(TankControllerUnreliable))]
    [RequireComponent(typeof(NetworkTransformUnreliable))]
    public class TankTurretUnreliable : TankTurretBase
    {
        [Header("Components")]
        public NetworkTransformUnreliable turretNTR;
        public NetworkTransformUnreliable barrelNTR;

        protected override void Reset()
        {
            base.Reset();

            // The base Tank uses the first NetworkTransformReliable for the tank body
            // Add additional NetworkTransformReliable components for the turret and barrel
            // Set SyncPosition to false because we only want to sync rotation
            NetworkTransformUnreliable[] NTs = GetComponents<NetworkTransformUnreliable>();

            if (NTs.Length < 2)
            {
                turretNTR = gameObject.AddComponent<NetworkTransformUnreliable>();
                turretNTR.transform.SetSiblingIndex(NTs[0].transform.GetSiblingIndex() + 1);
                NTs = GetComponents<NetworkTransformUnreliable>();
            }
            else
                turretNTR = NTs[1];

            // Ensure SyncDirection is Client to Server
            turretNTR.syncDirection = SyncDirection.ClientToServer;
            turretNTR.syncPosition = false;

            // Set SyncPosition to false because we only want to sync rotation
            //turretNTR.syncPosition = false;

            if (base.turret != null)
                turretNTR.target = turret;

            if (NTs.Length < 3)
            {
                barrelNTR = gameObject.AddComponent<NetworkTransformUnreliable>();
                barrelNTR.transform.SetSiblingIndex(NTs[1].transform.GetSiblingIndex() + 1);
                NTs = GetComponents<NetworkTransformUnreliable>();
            }
            else
                barrelNTR = NTs[2];

            // Ensure SyncDirection is Client to Server
            barrelNTR.syncDirection = SyncDirection.ClientToServer;
            barrelNTR.syncPosition = false;

            // Set SyncPosition to false because we only want to sync rotation
            //barrelNTR.syncPosition = false;

            if (barrel != null)
                barrelNTR.target = barrel;
        }
    }
}
