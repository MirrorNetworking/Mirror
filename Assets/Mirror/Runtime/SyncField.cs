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
// TODO OR embrace implicit construction:
//      * struct instead of class, otherwise health++ creates new class
//        BUT how to store in syncobjects list?
//      * copy old field's OnDirty callback to the new field
//      * AND call OnDirty if we reassigned it
//      * then .health++ works
//      =========> BUT: serialization iterates SyncObjects. so needs to be class.
// TODO OnChanged hook
// TODO tests
using System;

namespace Mirror
{
    // * needs to be a 'class' so that we can track it in SyncObjects list, and
    //   iterate it for de/serialization.
    // * should be 'readonly' so nobody assigns monsterA.field = monsterB.field.
    public class SyncField<T> : SyncObject
    {
        T _Value;
        public T Value
        {
            get => _Value;
            set
            {
                // set value, set dirty bit
                // TODO only if changed, see NetworkBehaviour.SyncVarEqual<T>
                _Value = value;
                OnDirty();
            }
        }

        // OnDirty sets the owner NetworkBehaviour's dirty bit
        public Action OnDirty { get; set; }

        // some SyncObject interface methods are unnecessary here
        public Func<bool> IsRecording { get; set; }
        public void ClearChanges() {}
        public void Reset() {}

        // ctor from value <T>
        public SyncField(T value) => _Value = value;

        // copy ctor
        public SyncField(SyncField<T> field)
        {
            // TODO: what should this do?
            throw new NotImplementedException();
        }

        // implicit conversion: int value = SyncField<T>
        public static implicit operator T(SyncField<T> field) => field.Value;

        // implicit conversion: SyncField<T> = value
        public static implicit operator SyncField<T>(T value) => new SyncField<T>(value);

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
