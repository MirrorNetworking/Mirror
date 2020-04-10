namespace Mirror.Examples
{
    public class Health : NetworkBehaviour
    {
        [SyncVar] public int health = 10;

        [ServerCallback]
        public void Update()
        {
            health = (health + 1) % 10;
        }
    }
}
