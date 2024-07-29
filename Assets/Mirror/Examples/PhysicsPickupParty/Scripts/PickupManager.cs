using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Mirror.Examples.PhysicsPickupParty
{
    public class PickupManager : NetworkBehaviour
    {
        public SceneReference sceneReference;
        public GameObject[] pickupArray;
        public Transform spawnPoint;
        public int maxSpawns = 10;
        public int currentSpawns = 0;
        public float spawnInterval = 5;
        public int spawnRange = 4;

        //public override void OnStartServer()
        //{
        // StartPickupInterval is called via start game timer, so objects do not appear upon server start
        //    if you dont want objects in the scene to begin with, remove them, call X amount here (optional)
        //}

        public IEnumerator StartPickupInterval()
        {
            while (currentSpawns < maxSpawns)
            {
                yield return new WaitForSeconds(spawnInterval);
                if (currentSpawns < maxSpawns)
                {
                    // extra check in case amounts changed since timers waited
                    SpawnPickup();
                    currentSpawns += 1;
                }
            }
        }

        public void SpawnPickup()
        {
            Vector3 randomPosition = new Vector3(
                Random.Range(spawnPoint.position.x - spawnRange, spawnPoint.position.x + spawnRange),
                spawnPoint.position.y,
                Random.Range(spawnPoint.position.z - spawnRange, spawnPoint.position.z + spawnRange));

            Quaternion randomRotation = Random.rotation;

            GameObject networkedPickup = Instantiate(pickupArray[Random.Range(0, pickupArray.Length)],
                randomPosition,
                randomRotation);
            NetworkServer.Spawn(networkedPickup);
            RpcPlayAudio();
        }

        [ClientRpc]
        public void RpcPlayAudio()
        {
            PlayAudio();
        }

        public void PlayAudio()
        {
            sceneReference.spawnPickupSound.Play();
        }
    }
}