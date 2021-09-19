// SyncField<T> to make [SyncVar] weaving easier.
//
// we can possibly move a lot of complex logic out of weaver:
//   * set dirty bit
//   * calling the hook
//   * hook guard in host mode
//
// here is the plan:
//   1. develop SyncField<T> along side [SyncVar]
//   2. internally replace [SyncVar]s with SyncField<T>
//   3. eventuall obsolete [SyncVar]
namespace Mirror
{
    // should be 'readonly' so nobody assigns monsterA.field = monsterB.field.
    // needs to be a 'class' so that we can track it in SyncObjects list.
    public class SyncField<T>
    {
        public T Value;
    }
}
