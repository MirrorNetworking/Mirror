// standalone utility functions for PredictedRigidbody component.
using System;
using UnityEngine;

namespace Mirror
{
    public static class PredictionUtils
    {
        // rigidbody ///////////////////////////////////////////////////////////
        // move a Rigidbody + settings from one GameObject to another.
        public static void MoveRigidbody(GameObject source, GameObject destination, bool destroySource = true)
        {
            // create a new Rigidbody component on destination.
            // note that adding a Joint automatically adds a Rigidbody.
            // so first check if one was added yet.
            Rigidbody original = source.GetComponent<Rigidbody>();
            if (original == null) throw new Exception($"Prediction: attempted to move {source}'s Rigidbody to the predicted copy, but there was no component.");
            Rigidbody rigidbodyCopy;
            if (!destination.TryGetComponent(out rigidbodyCopy))
                rigidbodyCopy = destination.AddComponent<Rigidbody>();

            // copy all properties
            rigidbodyCopy.mass = original.mass;
#if UNITY_6000_0_OR_NEWER
            rigidbodyCopy.linearDamping = original.linearDamping;
            rigidbodyCopy.angularDamping = original.angularDamping;
#else
            rigidbodyCopy.drag = original.drag;
            rigidbodyCopy.angularDrag = original.angularDrag;
#endif
            rigidbodyCopy.useGravity = original.useGravity;
            rigidbodyCopy.isKinematic = original.isKinematic;
            rigidbodyCopy.interpolation = original.interpolation;
            rigidbodyCopy.collisionDetectionMode = original.collisionDetectionMode;
            rigidbodyCopy.constraints = original.constraints;
            rigidbodyCopy.sleepThreshold = original.sleepThreshold;
            rigidbodyCopy.freezeRotation = original.freezeRotation;

            // moving (Configurable)Joints messes up their range of motion unless
            // we reset to initial position first (we do this in PredictedRigibody.cs).
            // so here we don't set the Rigidbody's physics position at all.
            // rigidbodyCopy.position = original.position;
            // rigidbodyCopy.rotation = original.rotation;

            // projects may keep Rigidbodies as kinematic sometimes. in that case, setting velocity would log an error
            if (!original.isKinematic)
            {
#if UNITY_6000_0_OR_NEWER
                rigidbodyCopy.linearVelocity = original.linearVelocity;
#else
                rigidbodyCopy.velocity = original.velocity;
#endif
                rigidbodyCopy.angularVelocity = original.angularVelocity;
            }

            // destroy original
            if (destroySource) GameObject.Destroy(original);
        }

        // helper function: if a collider is on a child, copy that child first.
        // this way child's relative position/rotation/scale are preserved.
        public static GameObject CopyRelativeTransform(GameObject source, Transform sourceChild, GameObject destination)
        {
            // is this on the source root? then we want to put it on the destination root.
            if (sourceChild == source.transform) return destination;

            // is this on a child? then create the same child with the same transform on destination.
            // note this is technically only correct for the immediate child since
            // .localPosition is relative to parent, but this is good enough.
            GameObject child = new GameObject(sourceChild.name);
            child.transform.SetParent(destination.transform, true);
            child.transform.localPosition = sourceChild.localPosition;
            child.transform.localRotation = sourceChild.localRotation;
            child.transform.localScale = sourceChild.localScale;

            // assign the same Layer for the physics copy.
            // games may use a custom physics collision matrix, layer matters.
            child.layer = sourceChild.gameObject.layer;

            return child;
        }

        // colliders ///////////////////////////////////////////////////////////
        // move all BoxColliders + settings from one GameObject to another.
        public static void MoveBoxColliders(GameObject source, GameObject destination, bool destroySource = true)
        {
            // colliders may be on children
            BoxCollider[] sourceColliders = source.GetComponentsInChildren<BoxCollider>();
            foreach (BoxCollider sourceCollider in sourceColliders)
            {
                // copy the relative transform:
                // if collider is on root, it returns destination root.
                // if collider is on a child, it creates and returns a child on destination.
                GameObject target = CopyRelativeTransform(source, sourceCollider.transform, destination);
                BoxCollider colliderCopy = target.AddComponent<BoxCollider>();
                colliderCopy.center = sourceCollider.center;
                colliderCopy.size = sourceCollider.size;
                colliderCopy.isTrigger = sourceCollider.isTrigger;
                colliderCopy.material = sourceCollider.material;
                if (destroySource) GameObject.Destroy(sourceCollider);
            }
        }

        // move all SphereColliders + settings from one GameObject to another.
        public static void MoveSphereColliders(GameObject source, GameObject destination, bool destroySource = true)
        {
            // colliders may be on children
            SphereCollider[] sourceColliders = source.GetComponentsInChildren<SphereCollider>();
            foreach (SphereCollider sourceCollider in sourceColliders)
            {
                // copy the relative transform:
                // if collider is on root, it returns destination root.
                // if collider is on a child, it creates and returns a child on destination.
                GameObject target = CopyRelativeTransform(source, sourceCollider.transform, destination);
                SphereCollider colliderCopy = target.AddComponent<SphereCollider>();
                colliderCopy.center = sourceCollider.center;
                colliderCopy.radius = sourceCollider.radius;
                colliderCopy.isTrigger = sourceCollider.isTrigger;
                colliderCopy.material = sourceCollider.material;
                if (destroySource) GameObject.Destroy(sourceCollider);
            }
        }

        // move all CapsuleColliders + settings from one GameObject to another.
        public static void MoveCapsuleColliders(GameObject source, GameObject destination, bool destroySource = true)
        {
            // colliders may be on children
            CapsuleCollider[] sourceColliders = source.GetComponentsInChildren<CapsuleCollider>();
            foreach (CapsuleCollider sourceCollider in sourceColliders)
            {
                // copy the relative transform:
                // if collider is on root, it returns destination root.
                // if collider is on a child, it creates and returns a child on destination.
                GameObject target = CopyRelativeTransform(source, sourceCollider.transform, destination);
                CapsuleCollider colliderCopy = target.AddComponent<CapsuleCollider>();
                colliderCopy.center = sourceCollider.center;
                colliderCopy.radius = sourceCollider.radius;
                colliderCopy.height = sourceCollider.height;
                colliderCopy.direction = sourceCollider.direction;
                colliderCopy.isTrigger = sourceCollider.isTrigger;
                colliderCopy.material = sourceCollider.material;
                if (destroySource) GameObject.Destroy(sourceCollider);
            }
        }

        // move all MeshColliders + settings from one GameObject to another.
        public static void MoveMeshColliders(GameObject source, GameObject destination, bool destroySource = true)
        {
            // colliders may be on children
            MeshCollider[] sourceColliders = source.GetComponentsInChildren<MeshCollider>();
            foreach (MeshCollider sourceCollider in sourceColliders)
            {
                // when Models have Mesh->Read/Write disabled, it means that Unity
                // uploads the mesh directly to the GPU and erases it on the CPU.
                // on some platforms this makes moving a MeshCollider in builds impossible:
                //
                //   "CollisionMeshData couldn't be created because the mesh has been marked as non-accessible."
                //
                // on other platforms, this works fine.
                // let's show an explicit log message so in case collisions don't
                // work at runtime, it's obvious why it happens and how to fix it.
                if (!sourceCollider.sharedMesh.isReadable)
                {
                    Debug.Log($"[Prediction]: MeshCollider on {sourceCollider.name} isn't readable, which may indicate that the Mesh only exists on the GPU. If {sourceCollider.name} is missing collisions, then please select the model in the Project Area, and enable Mesh->Read/Write so it's also available on the CPU!");
                    // don't early return. keep trying, it may work.
                }

                // copy the relative transform:
                // if collider is on root, it returns destination root.
                // if collider is on a child, it creates and returns a child on destination.
                GameObject target = CopyRelativeTransform(source, sourceCollider.transform, destination);
                MeshCollider colliderCopy = target.AddComponent<MeshCollider>();
                colliderCopy.sharedMesh = sourceCollider.sharedMesh;
                colliderCopy.convex = sourceCollider.convex;
                colliderCopy.isTrigger = sourceCollider.isTrigger;
                colliderCopy.material = sourceCollider.material;
                if (destroySource) GameObject.Destroy(sourceCollider);
            }
        }

        // move all Colliders + settings from one GameObject to another.
        public static void MoveAllColliders(GameObject source, GameObject destination, bool destroySource = true)
        {
            MoveBoxColliders(source, destination, destroySource);
            MoveSphereColliders(source, destination, destroySource);
            MoveCapsuleColliders(source, destination, destroySource);
            MoveMeshColliders(source, destination, destroySource);
        }

        // joints //////////////////////////////////////////////////////////////
        // move all CharacterJoints + settings from one GameObject to another.
        public static void MoveCharacterJoints(GameObject source, GameObject destination, bool destroySource = true)
        {
            // colliders may be on children
            CharacterJoint[] sourceJoints = source.GetComponentsInChildren<CharacterJoint>();
            foreach (CharacterJoint sourceJoint in sourceJoints)
            {
            // copy the relative transform:
                // if joint is on root, it returns destination root.
                // if joint is on a child, it creates and returns a child on destination.
                GameObject target = CopyRelativeTransform(source, sourceJoint.transform, destination);
                CharacterJoint jointCopy = target.AddComponent<CharacterJoint>();
                // apply settings, in alphabetical order
                jointCopy.anchor = sourceJoint.anchor;
                jointCopy.autoConfigureConnectedAnchor = sourceJoint.autoConfigureConnectedAnchor;
                jointCopy.axis = sourceJoint.axis;
                jointCopy.breakForce = sourceJoint.breakForce;
                jointCopy.breakTorque = sourceJoint.breakTorque;
                jointCopy.connectedAnchor = sourceJoint.connectedAnchor;
                jointCopy.connectedBody = sourceJoint.connectedBody;
                jointCopy.connectedMassScale = sourceJoint.connectedMassScale;
                jointCopy.enableCollision = sourceJoint.enableCollision;
                jointCopy.enablePreprocessing = sourceJoint.enablePreprocessing;
                jointCopy.enableProjection = sourceJoint.enableProjection;
                jointCopy.highTwistLimit = sourceJoint.highTwistLimit;
                jointCopy.lowTwistLimit = sourceJoint.lowTwistLimit;
                jointCopy.massScale = sourceJoint.massScale;
                jointCopy.projectionAngle = sourceJoint.projectionAngle;
                jointCopy.projectionDistance = sourceJoint.projectionDistance;
                jointCopy.swing1Limit = sourceJoint.swing1Limit;
                jointCopy.swing2Limit = sourceJoint.swing2Limit;
                jointCopy.swingAxis = sourceJoint.swingAxis;
                jointCopy.swingLimitSpring = sourceJoint.swingLimitSpring;
                jointCopy.twistLimitSpring = sourceJoint.twistLimitSpring;
#if UNITY_2020_3_OR_NEWER
                jointCopy.connectedArticulationBody = sourceJoint.connectedArticulationBody;
#endif

                if (destroySource) GameObject.Destroy(sourceJoint);
            }
        }

        // move all ConfigurableJoints + settings from one GameObject to another.
        public static void MoveConfigurableJoints(GameObject source, GameObject destination, bool destroySource = true)
        {
            // colliders may be on children
            ConfigurableJoint[] sourceJoints = source.GetComponentsInChildren<ConfigurableJoint>();
            foreach (ConfigurableJoint sourceJoint in sourceJoints)
            {
                // copy the relative transform:
                // if joint is on root, it returns destination root.
                // if joint is on a child, it creates and returns a child on destination.
                GameObject target = CopyRelativeTransform(source, sourceJoint.transform, destination);
                ConfigurableJoint jointCopy = target.AddComponent<ConfigurableJoint>();
                // apply settings, in alphabetical order
                jointCopy.anchor = sourceJoint.anchor;
                jointCopy.angularXLimitSpring = sourceJoint.angularXLimitSpring;
                jointCopy.angularXDrive = sourceJoint.angularXDrive;
                jointCopy.angularXMotion = sourceJoint.angularXMotion;
                jointCopy.angularYLimit = sourceJoint.angularYLimit;
                jointCopy.angularYMotion = sourceJoint.angularYMotion;
                jointCopy.angularYZDrive = sourceJoint.angularYZDrive;
                jointCopy.angularYZLimitSpring = sourceJoint.angularYZLimitSpring;
                jointCopy.angularZLimit = sourceJoint.angularZLimit;
                jointCopy.angularZMotion = sourceJoint.angularZMotion;
                jointCopy.autoConfigureConnectedAnchor = sourceJoint.autoConfigureConnectedAnchor;
                jointCopy.axis = sourceJoint.axis;
                jointCopy.breakForce = sourceJoint.breakForce;
                jointCopy.breakTorque = sourceJoint.breakTorque;
                jointCopy.configuredInWorldSpace = sourceJoint.configuredInWorldSpace;
                jointCopy.connectedAnchor = sourceJoint.connectedAnchor;
                jointCopy.connectedBody = sourceJoint.connectedBody;
                jointCopy.connectedMassScale = sourceJoint.connectedMassScale;
                jointCopy.enableCollision = sourceJoint.enableCollision;
                jointCopy.enablePreprocessing = sourceJoint.enablePreprocessing;
                jointCopy.highAngularXLimit = sourceJoint.highAngularXLimit; // moving this only works if the object is at initial position/rotation/scale, see PredictedRigidbody.cs
                jointCopy.linearLimitSpring = sourceJoint.linearLimitSpring;
                jointCopy.linearLimit = sourceJoint.linearLimit;
                jointCopy.lowAngularXLimit = sourceJoint.lowAngularXLimit;   // moving this only works if the object is at initial position/rotation/scale, see PredictedRigidbody.cs
                jointCopy.massScale = sourceJoint.massScale;
                jointCopy.projectionAngle = sourceJoint.projectionAngle;
                jointCopy.projectionDistance = sourceJoint.projectionDistance;
                jointCopy.projectionMode = sourceJoint.projectionMode;
                jointCopy.rotationDriveMode = sourceJoint.rotationDriveMode;
                jointCopy.secondaryAxis = sourceJoint.secondaryAxis;
                jointCopy.slerpDrive = sourceJoint.slerpDrive;
                jointCopy.swapBodies = sourceJoint.swapBodies;
                jointCopy.targetAngularVelocity = sourceJoint.targetAngularVelocity;
                jointCopy.targetPosition = sourceJoint.targetPosition;
                jointCopy.targetRotation = sourceJoint.targetRotation;
                jointCopy.targetVelocity = sourceJoint.targetVelocity;
                jointCopy.xDrive = sourceJoint.xDrive;
                jointCopy.xMotion = sourceJoint.xMotion;
                jointCopy.yDrive = sourceJoint.yDrive;
                jointCopy.yMotion = sourceJoint.yMotion;
                jointCopy.zDrive = sourceJoint.zDrive;
                jointCopy.zMotion = sourceJoint.zMotion;
#if UNITY_2020_3_OR_NEWER
                jointCopy.connectedArticulationBody = sourceJoint.connectedArticulationBody;
#endif

                if (destroySource) GameObject.Destroy(sourceJoint);
            }
        }

        // move all FixedJoints + settings from one GameObject to another.
        public static void MoveFixedJoints(GameObject source, GameObject destination, bool destroySource = true)
        {
            // colliders may be on children
            FixedJoint[] sourceJoints = source.GetComponentsInChildren<FixedJoint>();
            foreach (FixedJoint sourceJoint in sourceJoints)
            {
                // copy the relative transform:
                // if joint is on root, it returns destination root.
                // if joint is on a child, it creates and returns a child on destination.
                GameObject target = CopyRelativeTransform(source, sourceJoint.transform, destination);
                FixedJoint jointCopy = target.AddComponent<FixedJoint>();
                // apply settings, in alphabetical order
                jointCopy.anchor = sourceJoint.anchor;
                jointCopy.autoConfigureConnectedAnchor = sourceJoint.autoConfigureConnectedAnchor;
                jointCopy.axis = sourceJoint.axis;
                jointCopy.breakForce = sourceJoint.breakForce;
                jointCopy.breakTorque = sourceJoint.breakTorque;
                jointCopy.connectedAnchor = sourceJoint.connectedAnchor;
                jointCopy.connectedBody = sourceJoint.connectedBody;
                jointCopy.connectedMassScale = sourceJoint.connectedMassScale;
                jointCopy.enableCollision = sourceJoint.enableCollision;
                jointCopy.enablePreprocessing = sourceJoint.enablePreprocessing;
                jointCopy.massScale = sourceJoint.massScale;
#if UNITY_2020_3_OR_NEWER
                jointCopy.connectedArticulationBody = sourceJoint.connectedArticulationBody;
#endif

                if (destroySource) GameObject.Destroy(sourceJoint);
            }
        }

        // move all HingeJoints + settings from one GameObject to another.
        public static void MoveHingeJoints(GameObject source, GameObject destination, bool destroySource = true)
        {
            // colliders may be on children
            HingeJoint[] sourceJoints = source.GetComponentsInChildren<HingeJoint>();
            foreach (HingeJoint sourceJoint in sourceJoints)
            {
                // copy the relative transform:
                // if joint is on root, it returns destination root.
                // if joint is on a child, it creates and returns a child on destination.
                GameObject target = CopyRelativeTransform(source, sourceJoint.transform, destination);
                HingeJoint jointCopy = target.AddComponent<HingeJoint>();
                // apply settings, in alphabetical order
                jointCopy.anchor = sourceJoint.anchor;
                jointCopy.autoConfigureConnectedAnchor = sourceJoint.autoConfigureConnectedAnchor;
                jointCopy.axis = sourceJoint.axis;
                jointCopy.breakForce = sourceJoint.breakForce;
                jointCopy.breakTorque = sourceJoint.breakTorque;
                jointCopy.connectedAnchor = sourceJoint.connectedAnchor;
                jointCopy.connectedBody = sourceJoint.connectedBody;
                jointCopy.connectedMassScale = sourceJoint.connectedMassScale;
                jointCopy.enableCollision = sourceJoint.enableCollision;
                jointCopy.enablePreprocessing = sourceJoint.enablePreprocessing;
                jointCopy.limits = sourceJoint.limits;
                jointCopy.massScale = sourceJoint.massScale;
                jointCopy.motor = sourceJoint.motor;
                jointCopy.spring = sourceJoint.spring;
                jointCopy.useLimits = sourceJoint.useLimits;
                jointCopy.useMotor = sourceJoint.useMotor;
                jointCopy.useSpring = sourceJoint.useSpring;
#if UNITY_2020_3_OR_NEWER
                jointCopy.connectedArticulationBody = sourceJoint.connectedArticulationBody;
#endif
#if UNITY_2022_3_OR_NEWER
                jointCopy.extendedLimits = sourceJoint.extendedLimits;
                jointCopy.useAcceleration = sourceJoint.useAcceleration;
#endif

                if (destroySource) GameObject.Destroy(sourceJoint);
            }
        }

        // move all SpringJoints + settings from one GameObject to another.
        public static void MoveSpringJoints(GameObject source, GameObject destination, bool destroySource = true)
        {
            // colliders may be on children
            SpringJoint[] sourceJoints = source.GetComponentsInChildren<SpringJoint>();
            foreach (SpringJoint sourceJoint in sourceJoints)
            {
                // copy the relative transform:
                // if joint is on root, it returns destination root.
                // if joint is on a child, it creates and returns a child on destination.
                GameObject target = CopyRelativeTransform(source, sourceJoint.transform, destination);
                SpringJoint jointCopy = target.AddComponent<SpringJoint>();
                // apply settings, in alphabetical order
                jointCopy.anchor = sourceJoint.anchor;
                jointCopy.autoConfigureConnectedAnchor = sourceJoint.autoConfigureConnectedAnchor;
                jointCopy.axis = sourceJoint.axis;
                jointCopy.breakForce = sourceJoint.breakForce;
                jointCopy.breakTorque = sourceJoint.breakTorque;
                jointCopy.connectedAnchor = sourceJoint.connectedAnchor;
                jointCopy.connectedBody = sourceJoint.connectedBody;
                jointCopy.connectedMassScale = sourceJoint.connectedMassScale;
                jointCopy.damper = sourceJoint.damper;
                jointCopy.enableCollision = sourceJoint.enableCollision;
                jointCopy.enablePreprocessing = sourceJoint.enablePreprocessing;
                jointCopy.massScale = sourceJoint.massScale;
                jointCopy.maxDistance = sourceJoint.maxDistance;
                jointCopy.minDistance = sourceJoint.minDistance;
                jointCopy.spring = sourceJoint.spring;
                jointCopy.tolerance = sourceJoint.tolerance;
#if UNITY_2020_3_OR_NEWER
                jointCopy.connectedArticulationBody = sourceJoint.connectedArticulationBody;
#endif

                if (destroySource) GameObject.Destroy(sourceJoint);
            }
        }

        // move all Joints + settings from one GameObject to another.
        public static void MoveAllJoints(GameObject source, GameObject destination, bool destroySource = true)
        {
            MoveCharacterJoints(source, destination, destroySource);
            MoveConfigurableJoints(source, destination, destroySource);
            MoveFixedJoints(source, destination, destroySource);
            MoveHingeJoints(source, destination, destroySource);
            MoveSpringJoints(source, destination, destroySource);
        }

        // all /////////////////////////////////////////////////////////////////
        // move all physics components from one GameObject to another.
        public static void MovePhysicsComponents(GameObject source, GameObject destination, bool destroySource = true)
        {
            // need to move joints first, otherwise we might see:
            // 'can't move Rigidbody because a Joint depends on it'
            MoveAllJoints(source, destination, destroySource);
            MoveAllColliders(source, destination, destroySource);
            MoveRigidbody(source, destination, destroySource);
        }
    }
}
