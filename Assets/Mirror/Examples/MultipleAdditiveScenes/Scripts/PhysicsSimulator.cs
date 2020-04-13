using UnityEngine;

namespace Mirror.Examples.MultipleAdditiveScenes
{
    public class PhysicsSimulator : MonoBehaviour
    {
        public PhysicsScene physicsScene;
        public PhysicsScene2D physicsScene2D;

        void Awake()
        {
            if (NetworkServer.active)
            {
                enabled = true;

                physicsScene = gameObject.scene.GetPhysicsScene();
                physicsScene2D = gameObject.scene.GetPhysicsScene2D();
            }
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active) return;

            if (physicsScene.IsValid() && physicsScene != Physics.defaultPhysicsScene)
                physicsScene.Simulate(Time.fixedDeltaTime);

            if (physicsScene2D.IsValid() && physicsScene2D != Physics2D.defaultPhysicsScene)
                physicsScene2D.Simulate(Time.fixedDeltaTime);
        }
    }
}
