using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("Network/Tank Turret (Hybrid)")]
    [RequireComponent(typeof(TankControllerHybrid))]
    [RequireComponent(typeof(NetworkTransformHybrid))]
    public class TankTurretHybrid : TankTurretBase
    {
        [Header("Network Transforms")]
        public NetworkTransformHybrid turretNetworkTransform;
        public NetworkTransformHybrid barrelNetworkTransform;

        protected override void Reset()
        {
            base.Reset();

            // The base Tank uses the first NetworkTransformHybrid for the tank body
            // Add additional NetworkTransformHybrid components for the turret and barrel
            // Set SyncPosition to false because we only want to sync rotation
            NetworkTransformHybrid[] NTs = GetComponents<NetworkTransformHybrid>();

            if (NTs.Length < 2)
            {
                turretNetworkTransform = gameObject.AddComponent<NetworkTransformHybrid>();
                turretNetworkTransform.transform.SetSiblingIndex(NTs[0].transform.GetSiblingIndex() + 1);
                NTs = GetComponents<NetworkTransformHybrid>();
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
                barrelNetworkTransform = gameObject.AddComponent<NetworkTransformHybrid>();
                barrelNetworkTransform.transform.SetSiblingIndex(NTs[1].transform.GetSiblingIndex() + 1);
                NTs = GetComponents<NetworkTransformHybrid>();
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
