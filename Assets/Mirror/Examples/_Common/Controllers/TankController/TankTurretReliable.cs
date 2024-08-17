using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("Network/Tank Turret (Reliable)")]
    [RequireComponent(typeof(TankControllerReliable))]
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class TankTurretReliable : TankTurretBase
    {
        [Header("Components")]
        public NetworkTransformReliable turretNTR;
        public NetworkTransformReliable barrelNTR;

        protected override void Reset()
        {
            base.Reset();

            // The base Tank uses the first NetworkTransformReliable for the tank body
            // Add additional NetworkTransformReliable components for the turret and barrel
            // Set SyncPosition to false because we only want to sync rotation
            NetworkTransformReliable[] NTs = GetComponents<NetworkTransformReliable>();

            if (NTs.Length < 2)
            {
                turretNTR = gameObject.AddComponent<NetworkTransformReliable>();
                turretNTR.transform.SetSiblingIndex(NTs[0].transform.GetSiblingIndex() + 1);
                NTs = GetComponents<NetworkTransformReliable>();
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
                barrelNTR = gameObject.AddComponent<NetworkTransformReliable>();
                barrelNTR.transform.SetSiblingIndex(NTs[1].transform.GetSiblingIndex() + 1);
                NTs = GetComponents<NetworkTransformReliable>();
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
