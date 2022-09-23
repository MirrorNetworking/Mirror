// SyncVar<T> to make [SyncVar] weaving easier.
//
// we can possibly move a lot of complex logic out of weaver:
//   * set dirty bit
//   * calling the hook
//   * hook guard in host mode
//   * GameObject/NetworkIdentity internal netId storage
//
// here is the plan:
//   1. develop SyncVar<T> along side [SyncVar]
//   2. internally replace [SyncVar]s with SyncVar<T>
//   3. eventually obsolete [SyncVar]
//
// downsides:
//   - generic <T> types don't show in Unity Inspector
//
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    // 'class' so that we can track it in SyncObjects list, and iterate it for
    //   de/serialization.
    [Serializable]
    public class SyncVar<T> : SyncObject, IEquatable<T>
    {
        // Unity 2020+ can show [SerializeField]<T> in inspector.
        // (only if SyncVar<T> isn't readonly though)
        [SerializeField] T _Value;

        // Value property with hooks
        // virtual for SyncFieldNetworkIdentity netId trick etc.
        public virtual T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _Value;
            set
            {
                // only if value changed. otherwise don't dirty/hook.
                // we have .Equals(T), simply reuse it here.
                if (!Equals(value))
                {
                    // set value, set dirty bit
                    T old = _Value;
                    _Value = value;
                    OnDirty();

                    // Value.set calls the hook if changed.
                    // calling Value.set from within the hook would call the
                    // hook again and deadlock. prevent it with hookGuard.
                    // (see test: Hook_Set_DoesntDeadlock)
                    if (!hookGuard &&
                        // original [SyncVar] only calls hook on clients.
                        // let's keep it for consistency for now
                        // TODO remove check & dependency in the future.
                        //      use isClient/isServer in the hook instead.
                        NetworkClient.active)
                    {
                        hookGuard = true;
                        InvokeCallback(old, value);
                        hookGuard = false;
                    }
                }
            }
        }

        // OnChanged Callback.
        // named 'Callback' for consistency with SyncList etc.
        // needs to be public so we can assign it in OnStartClient.
        // (ctor passing doesn't work, it can only take static functions)
        // assign via: field.Callback += ...!
        public event Action<T, T> Callback;

        // OnCallback is responsible for calling the callback.
        // this is necessary for inheriting classes like SyncVarGameObject,
        // where the netIds should be converted to GOs and call the GO hook.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void InvokeCallback(T oldValue, T newValue) =>
            Callback?.Invoke(oldValue, newValue);

        // Value.set calls the hook if changed.
        // calling Value.set from within the hook would call the hook again and
        // deadlock. prevent it with a simple 'are we inside the hook' bool.
        bool hookGuard;

        public override void ClearChanges() {}
        public override void Reset() {}

        // ctor from value <T> and OnChanged hook.
        // it was always called 'hook'. let's keep naming for convenience.
        public SyncVar(T value)
        {
            // recommend explicit GameObject, NetworkIdentity, NetworkBehaviour
            // with persistent netId method
            if (this is SyncVar<GameObject>)
                Debug.LogWarning($"Use explicit {nameof(SyncVarGameObject)} class instead of {nameof(SyncVar<T>)}<GameObject>. It stores netId internally for persistence.");

            if (this is SyncVar<NetworkIdentity>)
                Debug.LogWarning($"Use explicit {nameof(SyncVarNetworkIdentity)} class instead of {nameof(SyncVar<T>)}<NetworkIdentity>. It stores netId internally for persistence.");

            if (this is SyncVar<NetworkBehaviour>)
                Debug.LogWarning($"Use explicit SyncVarNetworkBehaviour class instead of {nameof(SyncVar<T>)}<NetworkBehaviour>. It stores netId internally for persistence.");

            _Value = value;
        }

        // NOTE: copy ctor is unnecessary.
        // SyncVar<T>s are readonly and only initialized by 'Value' once.

        // implicit conversion: int value = SyncVar<T>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(SyncVar<T> field) => field.Value;

        // implicit conversion: SyncVar<T> = value
        // even if SyncVar<T> is readonly, it's still useful: SyncVar<int> = 1;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SyncVar<T>(T value) => new SyncVar<T>(value);

        // serialization (use .Value instead of _Value so hook is called!)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void OnSerializeAll(NetworkWriter writer) => writer.Write(Value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void OnSerializeDelta(NetworkWriter writer) => writer.Write(Value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void OnDeserializeAll(NetworkReader reader) => Value = reader.Read<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void OnDeserializeDelta(NetworkReader reader) => Value = reader.Read<T>();

        // IEquatable should compare Value.
        // SyncVar<T> should act invisibly like [SyncVar] before.
        // this way we can do SyncVar<int> health == 0 etc.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(T other) =>
            // from NetworkBehaviour.SyncVarEquals:
            // EqualityComparer method avoids allocations.
            // otherwise <T> would have to be :IEquatable (not all structs are)
            EqualityComparer<T>.Default.Equals(Value, other);

        // ToString should show Value.
        // SyncVar<T> should act invisibly like [SyncVar] before.
        public override string ToString() => Value.ToString();
    }
}
