using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class MoveRandomly : NetworkBehaviour
{
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(__Move());
    }

    private IEnumerator __Move()
    {
        while (true)
        {

            transform.position = Vector3.zero + (Random.insideUnitSphere * 3f);
            yield return new WaitForSeconds(2f);
        }
    }

}
