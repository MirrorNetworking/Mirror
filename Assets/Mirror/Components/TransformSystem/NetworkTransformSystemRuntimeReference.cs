using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror.TransformSyncing
{
    [CreateAssetMenu]
    public class NetworkTransformSystemRuntimeReference : ScriptableObject
    {
        NetworkTransformSystem _system;
        public NetworkTransformSystem System
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _system;
            set
            {
                // warn if both not null and not equal
                if (value != null && _system != null && _system != value)
                {
                    Debug.LogWarning($"{name} already had a system, old='{_system}' new='{value}'");
                }

                _system = value;
            }
        }
        public readonly Dictionary<uint, IHasPositionRotation> behaviours = new Dictionary<uint, IHasPositionRotation>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddBehaviour(IHasPositionRotation behaviour)
        {
            uint id = behaviour.State.id;
            Debug.Assert(id != 0, "Behaviour had no id");
            behaviours.Add(id, behaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveBehaviour(IHasPositionRotation behaviour)
        {
            uint id = behaviour.State.id;
            Debug.Assert(id != 0, "Behaviour had no id");
            behaviours.Remove(id);
        }
    }
}
