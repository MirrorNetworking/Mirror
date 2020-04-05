namespace Mirror.Examples
{
    public class Health : NetworkBehaviour
    {
        /// <summary>
        /// When false health updates every second causes it to be synced over network
        /// </summary>
        public static bool idle = false;

        [SyncVar] public int health = 10;

        [ServerCallback]
        public void Update()
        {
            if (idle) { return; }

            health = (health + 1) % 10;
        }
    }
}
