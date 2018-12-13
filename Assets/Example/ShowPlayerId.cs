﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class ShowPlayerId : NetworkBehaviour {

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
        text.text = "P " + netId + ":" + data ;
    }
}
