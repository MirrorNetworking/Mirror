using Mirror;
namespace WeaverSyncVarHookTests.ErrorWhenNewParametersInNewIsWrongType
{
    class ErrorWhenNewParametersInNewIsWrongType : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int newValue)
        {

        }
    }
}
