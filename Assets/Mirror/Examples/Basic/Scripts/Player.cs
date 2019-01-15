using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Mirror.Examples.Basic
{

    public class Player : NetworkBehaviour
    {

        [SyncVar]
        public int data;

        public TextMesh text;


        public override void OnStartServer()
        {
            base.OnStartServer();
            InvokeRepeating("UpdateData", 1, 1);
        }

        public void UpdateData()
        {
            data = Random.Range(0, 10);
        }

        public void Update()
        {
            if (isLocalPlayer)
                text.color = Color.red;

            text.text = string.Format("Player {0}\ndata={1}", netId, data);
        }
    }
}