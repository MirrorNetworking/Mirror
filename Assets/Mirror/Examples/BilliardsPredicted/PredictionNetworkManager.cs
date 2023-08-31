// prediction needs some custom setup.
// for example, we need to ensure Physics 'Auto Simulation' is disabled
// because we want to call Physics.Simulate() manually.
using UnityEngine;

namespace Mirror.Examples.BilliardsPredicted
{
    public class PredictionNetworkManager : NetworkManager
    {
        public override void Awake()
        {
            // Ensure Physics 'Auto Simulation' is disabled when starting this scene
            Physics.autoSimulation = false;
            Debug.Log($"Prediction: disabled Physics Auto Simulation to prepare for manual Simulation steps.");

            base.Awake();
        }
    }
}
