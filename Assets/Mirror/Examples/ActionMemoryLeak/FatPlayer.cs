using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Random = UnityEngine.Random;

public class FatPlayer : NetworkBehaviour
{
    private byte[] _fatData = new byte[1024 * 1024 * 64];
    [SyncVar(hook = nameof(OnStringDataChanged))]
    public string StringData;

    public override void OnStartServer()
    {
        transform.position = Random.insideUnitSphere;
    }

    private void Update()
    {
        StringData = Random.value.ToString("N");
    }

    void OnStringDataChanged(string _, string __)
    {
        Debug.Log($"Hook called! {StringData}, FatData is still {_fatData.Length} bytes");
    }
}
