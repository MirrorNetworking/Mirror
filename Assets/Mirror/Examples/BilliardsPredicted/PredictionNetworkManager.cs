// prediction needs some custom setup.
// for example, we need to ensure Physics 'Auto Simulation' is disabled
// because we want to call Physics.Simulate() manually.
using System;
using UnityEngine;

namespace Mirror.Examples.BilliardsPredicted
{
    public class PredictionNetworkManager : NetworkManager
    {
        // physics simulation isn't called automatically anymore.
        // count the update time and call it every fixedDeltaTime manually.
        double timeAccumulator = 0;

        public override void Awake()
        {
            // Ensure Physics 'Auto Simulation' is disabled when starting this scene
            Physics.autoSimulation = false;
            Debug.Log($"Prediction: disabled Physics Auto Simulation to prepare for manual Simulation steps.");

            base.Awake();
        }

        // 'fix your timestep' version with manual accumulator in update.
        // TODO this is still jittery.
        // TODO try to call this from NetworkEarly/LateUpdate. which is better?
        /*
        public override void Update()
        {
            base.Update();

            // 'Fix your Timestep' article
            // https://www.gafferongames.com/post/fix_your_timestep/
            // timeAccumulator += Time.deltaTime;
            // while (timeAccumulator >= Time.fixedDeltaTime)
            // {
            //     Physics.Simulate(Time.fixedDeltaTime);
            //     // subtract from accumulator.
            //     // note that -= fixedDeltaTime is jittery.
            //     // glenn fiedler's -= deltaTime is smooth.
            //     timeAccumulator -= Time.deltaTime;
            //     physicsTime += Time.fixedDeltaTime;
            // }

            // increase accumulator with deadlock protection.
            // only every step through up to 'maximumDeltaTime'.
            timeAccumulator += Math.Min(Time.deltaTime, Time.maximumDeltaTime);
            while (timeAccumulator >= Time.fixedDeltaTime)
            {
                NetworkPhysicsUpdate();
                timeAccumulator -= Time.fixedDeltaTime;
            }
        }
        */

        // simulating physics from FixedUpdate is the smoothest solution.
        // calling this from Update or LateUpdate causes noticeable jitter.
        // TODO try to call this from NetworkEarly/LateUpdate. which is better?
        protected void FixedUpdate()
        {
            // this is the smoothest solution.
            // automatically does the 'fixed timestep' calculations for us.
            NetworkPhysicsUpdate();
        }

        protected virtual void NetworkPhysicsUpdate()
        {
            Physics.Simulate(Time.fixedDeltaTime);

        }
    }
}
