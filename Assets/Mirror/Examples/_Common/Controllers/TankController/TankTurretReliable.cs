using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("Network/Tank Turret (Reliable)")]
    [RequireComponent(typeof(TankControllerReliable))]
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class TankTurretReliable : TankTurretBase
    {
        [Header("Network Transforms")]
        public NetworkTransformReliable turretNetworkTransform;
        public NetworkTransformReliable barrelNetworkTransform;

        protected override void Reset()
        {
            base.Reset();

            // The base Tank uses the first NetworkTransformReliable for the tank body
            // Add additional NetworkTransformReliable components for the turret and barrel
            // Set SyncPosition to false because we only want to sync rotation
            NetworkTransformReliable[] NTs = GetComponents<NetworkTransformReliable>();

            if (NTs.Length < 2)
            {
                turretNetworkTransform = gameObject.AddComponent<NetworkTransformReliable>();
                turretNetworkTransform.transform.SetSiblingIndex(NTs[0].transform.GetSiblingIndex() + 1);
                NTs = GetComponents<NetworkTransformReliable>();
            }
            else
                turretNetworkTransform = NTs[1];

            // Ensure syncDirection is Client to Server
            turretNetworkTransform.syncDirection = SyncDirection.ClientToServer;

            // Set syncPosition to false because we only want to sync rotation
            turretNetworkTransform.syncPosition = false;

            if (base.turret != null)
                turretNetworkTransform.target = turret;

            if (NTs.Length < 3)
            {
                barrelNetworkTransform = gameObject.AddComponent<NetworkTransformReliable>();
                barrelNetworkTransform.transform.SetSiblingIndex(NTs[1].transform.GetSiblingIndex() + 1);
                NTs = GetComponents<NetworkTransformReliable>();
            }
            else
                barrelNetworkTransform = NTs[2];

            // Ensure syncDirection is Client to Server
            barrelNetworkTransform.syncDirection = SyncDirection.ClientToServer;

            // Set syncPosition to false because we only want to sync rotation
            barrelNetworkTransform.syncPosition = false;

            if (barrel != null)
                barrelNetworkTransform.target = barrel;
        }
    }
}
