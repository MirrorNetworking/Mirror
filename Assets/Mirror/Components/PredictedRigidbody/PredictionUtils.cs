// standalone utility functions for PredictedRigidbody component.
using System;
using UnityEngine;

namespace Mirror
{
    public static class PredictionUtils
    {
        // rigidbody ///////////////////////////////////////////////////////////
        // move a Rigidbody + settings from one GameObject to another.
        public static void MoveRigidbody(GameObject source, GameObject destination)
        {
            // create a new Rigidbody component on destionation
            Rigidbody original = source.GetComponent<Rigidbody>();
            if (original == null) throw new Exception($"Prediction: attempted to move {source}'s Rigidbody to the predicted copy, but there was no component.");
            Rigidbody rigidbodyCopy = destination.AddComponent<Rigidbody>();

            // copy all properties
            rigidbodyCopy.mass = original.mass;
            rigidbodyCopy.drag = original.drag;
            rigidbodyCopy.angularDrag = original.angularDrag;
            rigidbodyCopy.useGravity = original.useGravity;
            rigidbodyCopy.isKinematic = original.isKinematic;
            rigidbodyCopy.interpolation = original.interpolation;
            rigidbodyCopy.collisionDetectionMode = original.collisionDetectionMode;
            rigidbodyCopy.constraints = original.constraints;
            rigidbodyCopy.sleepThreshold = original.sleepThreshold;
            rigidbodyCopy.freezeRotation = original.freezeRotation;
            rigidbodyCopy.position = original.position;
            rigidbodyCopy.rotation = original.rotation;
            rigidbodyCopy.velocity = original.velocity;

            // destroy original
            GameObject.Destroy(original);
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
        public static void MoveBoxColliders(GameObject source, GameObject destination)
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
                GameObject.Destroy(sourceCollider);
            }
        }

        // move all SphereColliders + settings from one GameObject to another.
        public static void MoveSphereColliders(GameObject source, GameObject destination)
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
                GameObject.Destroy(sourceCollider);
            }
        }

        // move all CapsuleColliders + settings from one GameObject to another.
        public static void MoveCapsuleColliders(GameObject source, GameObject destination)
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
                GameObject.Destroy(sourceCollider);
            }
        }

        // move all MeshColliders + settings from one GameObject to another.
        public static void MoveMeshColliders(GameObject source, GameObject destination)
        {
            // colliders may be on children
            MeshCollider[] sourceColliders = source.GetComponentsInChildren<MeshCollider>();
            foreach (MeshCollider sourceCollider in sourceColliders)
            {
                // copy the relative transform:
                // if collider is on root, it returns destination root.
                // if collider is on a child, it creates and returns a child on destination.
                GameObject target = CopyRelativeTransform(source, sourceCollider.transform, destination);
                MeshCollider colliderCopy = target.AddComponent<MeshCollider>();
                colliderCopy.sharedMesh = sourceCollider.sharedMesh;
                colliderCopy.convex = sourceCollider.convex;
                colliderCopy.isTrigger = sourceCollider.isTrigger;
                GameObject.Destroy(sourceCollider);
            }
        }

        // move all Colliders + settings from one GameObject to another.
        public static void MoveAllColliders(GameObject source, GameObject destination)
        {
            MoveBoxColliders(source, destination);
            MoveSphereColliders(source, destination);
            MoveCapsuleColliders(source, destination);
            MoveMeshColliders(source, destination);
        }

        // all /////////////////////////////////////////////////////////////////
        // move all physics components from one GameObject to another.
        public static void MovePhysicsComponents(GameObject source, GameObject destination)
        {
            MoveRigidbody(source, destination);
            MoveAllColliders(source, destination);
        }
    }
}
