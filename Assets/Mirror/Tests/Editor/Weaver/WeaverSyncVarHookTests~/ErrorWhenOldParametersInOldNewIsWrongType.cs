using Mirror;
namespace WeaverSyncVarHookTests.ErrorWhenOldParametersInOldNewIsWrongType
{
    class ErrorWhenOldParametersInOldNewIsWrongType : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onChangeHealth))]
        int health;

        void onChangeHealth(int newValue)
        {

        }
    }
}
