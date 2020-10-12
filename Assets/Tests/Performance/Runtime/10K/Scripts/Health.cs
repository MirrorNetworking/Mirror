namespace Mirror.Examples
{
    public class Health : NetworkBehaviour
    {
        [SyncVar] public int health = 10;

        [Server(error = false)]
        public void Update()
        {
            health = (health + 1) % 10;
        }
    }
}
