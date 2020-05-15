using Mirror;
namespace WeaverSyncVarHookTests.ErrorWhenNewParametersInOldNewIsWrongType
{
    class ErrorWhenNewParametersInOldNewIsWrongType : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int newValue)
        {

        }
    }
}
