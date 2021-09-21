// SyncField<T> to make [SyncVar] weaving easier.
//
// we can possibly move a lot of complex logic out of weaver:
//   * set dirty bit
//   * calling the hook
//   * hook guard in host mode
//   * GameObject/NetworkIdentity internal netId storage
//
// here is the plan:
//   1. develop SyncField<T> along side [SyncVar]
//   2. internally replace [SyncVar]s with SyncField<T>
//   3. eventually obsolete [SyncVar]
//
// downsides:
//   - generic <T> types don't show in Unity Inspector
//
// TODO force 'readonly' in Weaver. otherwise 'health--' sets a new field.
using System;
using System.Collections.Generic;

namespace Mirror
{
    // 'class' so that we can track it in SyncObjects list, and iterate it for
    //   de/serialization.
    // 'readonly' so nobody assigns monsterA.field = monsterB.field.
    // 'sealed' for now. prevents IEqualityComparer warning.
    public sealed class SyncField<T> : SyncObject, IEquatable<T>
    {
        T _Value;
        public T Value
        {
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
                    OnDirty?.Invoke();

                    // Value.set calls the hook if changed.
                    // calling Value.set from within the hook would call the
                    // hook again and deadlock. prevent it with hookGuard.
                    // (see test: Hook_Set_DoesntDeadlock)
                    if (hook != null && !hookGuard)
                    {
                        // Note that unlike [SyncVar], SyncField<T> hook is
                        // called on server & clients.
                        // use 'isServer' / 'isClient' early returns in the hook
                        // if necessary.
                        hookGuard = true;
                        hook(old, value);
                        hookGuard = false;
                    }
                }
            }
        }

        // OnChanged hook
        readonly Action<T, T> hook;

        // Value.set calls the hook if changed.
        // calling Value.set from within the hook would call the hook again and
        // deadlock. prevent it with a simple 'are we inside the hook' bool.
        bool hookGuard;

        // OnDirty sets the owner NetworkBehaviour's dirty bit
        public Action OnDirty { get; set; }

        // some SyncObject interface methods are unnecessary here
        public Func<bool> IsRecording { get; set; }
        public void ClearChanges() {}
        // Deprecated 2021-09-17
        [Obsolete("Deprecated: Use ClearChanges instead.")]
        public void Flush() => ClearChanges();
        public void Reset() {}

        // ctor from value <T> and OnChanged hook.
        // it was always called 'hook'. let's keep naming for convenience.
        public SyncField(T value, Action<T, T> hook = null)
        {
            _Value = value;
            this.hook = hook;
        }

        // NOTE: copy ctor is unnecessary.
        // SyncFields are readonly and only initialized by 'Value' once.

        // implicit conversion: int value = SyncField<T>
        public static implicit operator T(SyncField<T> field) => field.Value;

        // implicit conversion: SyncField<T> = value
        // even if SyncField is readonly, it's still useful: SyncField<int> = 1;
        public static implicit operator SyncField<T>(T value) => new SyncField<T>(value);

        // serialization (use .Value instead of _Value so hook is called!)
        public void OnSerializeAll(NetworkWriter writer) => writer.Write(Value);
        public void OnSerializeDelta(NetworkWriter writer) => writer.Write(Value);
        public void OnDeserializeAll(NetworkReader reader) => Value = reader.Read<T>();
        public void OnDeserializeDelta(NetworkReader reader) => Value = reader.Read<T>();

        // IEquatable should compare Value.
        // SyncField should act invisibly like [SyncVar] before.
        // this way we can do SyncField<int> health == 0 etc.
        public bool Equals(T other) =>
            // from NetworkBehaviour.SyncVarEquals:
            // EqualityComparer method avoids allocations.
            // otherwise <T> would have to be :IEquatable (not all structs are)
            EqualityComparer<T>.Default.Equals(Value, other);

        // ToString should show Value.
        // SyncField should act invisibly like [SyncVar] before.
        public override string ToString() => Value.ToString();
    }
}
