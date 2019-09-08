using UnityEngine;

namespace Mirror.Examples.NetworkRoom
{
    public class Spawner : NetworkBehaviour
    {
        public NetworkIdentity prizePrefab;

        float x;
        float z;

        GameObject newPrize;
        Reward reward;

        void Start()
        {
            for (int i = 0; i < 10; i++)
            {
                SpawnPrize();
            }
        }

        public void SpawnPrize()
        {
            x = Random.Range(-19, 20);
            z = Random.Range(-19, 20);

            newPrize = Instantiate(prizePrefab.gameObject, new Vector3(x, 1, z), Quaternion.identity);
            newPrize.name = prizePrefab.name;
            reward = newPrize.gameObject.GetComponent<Reward>();
            reward.spawner = this;
            reward.prizeColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);

            if (LogFilter.Debug) Debug.LogFormat("Spawning Prize R:{0} G:{1} B:{2}", reward.prizeColor.r, reward.prizeColor.g, reward.prizeColor.b);

            NetworkServer.Spawn(newPrize);
        }
    }
}
