namespace Mirror.Examples.Tanks
{
    public class Test : NetworkBehaviour
    {
        [SyncVar] public int health =4;

        void Fun()
        {
            SyncVar<int> test = SyncVar<int>.Create(health);
        }
    }
}
