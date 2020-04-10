using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Mirror.Examples.MultipleAdditiveScenes
{
    public class PhysicsSimulator : MonoBehaviour
    {
        public PhysicsScene physicsScene;
        private float timer;

        private void Awake()
        {
            enabled = (NetworkServer.active);
            physicsScene = gameObject.scene.GetPhysicsScene();
        }

        void Update()
        {
            if (!physicsScene.IsValid())
                return; // do nothing if the physics Scene is not valid.

            timer += Time.deltaTime;

            // Catch up with the game time.
            // Advance the physics simulation in portions of Time.fixedDeltaTime
            // Note that generally, we don't want to pass variable delta to Simulate as that leads to unstable results.
            while (timer >= Time.fixedDeltaTime)
            {
                timer -= Time.fixedDeltaTime;
                physicsScene.Simulate(Time.fixedDeltaTime);
            }
        }
    }
}
