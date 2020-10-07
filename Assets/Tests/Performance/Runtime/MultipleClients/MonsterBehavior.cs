using System.Collections;
using UnityEngine;

namespace Mirror.Tests.Performance.Runtime
{
    public class MonsterBehavior : NetworkBehaviour
    {
        [SyncVar]
        public Vector3 position;

        [SyncVar]
        public int MonsterId;

        public void Awake()
        {
            NetIdentity.OnStartServer.AddListener(StartServer);
            NetIdentity.OnStopServer.AddListener(StopServer);
        }

        private void StopServer()
        {
            StopAllCoroutines();
        }

        private void StartServer()
        {
            StartCoroutine(MoveMonster());
        }

        private IEnumerator MoveMonster()
        {
            while (true)
            {
                position = Random.insideUnitSphere;
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}