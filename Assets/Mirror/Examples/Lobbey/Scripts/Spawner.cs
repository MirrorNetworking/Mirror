using UnityEngine;

namespace Mirror.Examples.NetworkLobby
{
    public class Spawner : NetworkBehaviour
    {
        public NetworkIdentity prizePrefab;

        private void Start()
        {
            for (int i = 0; i < 10; i++)
            {
                SpawnPrize();
            }
        }

        public void SpawnPrize()
        {
            float x = Random.Range(-19, 20);
            float z = Random.Range(-19, 20);

            GameObject newPrize = Instantiate(prizePrefab.gameObject, new Vector3(x, 1, z), Quaternion.identity);
            Reward reward = newPrize.gameObject.GetComponent<Reward>();
            reward.spawner = this;
            reward.prizeColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);

            if (LogFilter.Debug) Debug.LogFormat("Spawning Prize R:{0} G:{1} B:{2}", reward.prizeColor.r, reward.prizeColor.g, reward.prizeColor.b);

            NetworkServer.Spawn(newPrize);
        }
    }
}
