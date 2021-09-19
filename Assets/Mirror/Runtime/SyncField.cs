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
using System;

namespace Mirror
{
    // should be 'readonly' so nobody assigns monsterA.field = monsterB.field.
    // needs to be a 'class' so that we can track it in SyncObjects list.
    public class SyncField<T> : SyncObject
    {
        T _Value;
        public T Value
        {
            get => _Value;
            set
            {
                _Value = value;
                OnDirty();
            }
        }

        public Action OnDirty { get; set; }

        // some SyncObject interface methods are unnecessary here
        public Func<bool> IsRecording { get; set; }
        public void ClearChanges() {}
        public void Reset() {}

        // ctor
        public SyncField(T value) => _Value = value;

        // serialization
        public void OnSerializeAll(NetworkWriter writer) => writer.Write(_Value);
        public void OnSerializeDelta(NetworkWriter writer) => writer.Write(_Value);
        public void OnDeserializeAll(NetworkReader reader)
        {
            _Value = reader.Read<T>();
        }
        public void OnDeserializeDelta(NetworkReader reader)
        {
            _Value = reader.Read<T>();
        }
    }
}
