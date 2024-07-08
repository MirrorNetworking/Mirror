using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System;

namespace Mirror.Examples.PhysicsPickupParty
{
    public class ZonesManager : NetworkBehaviour
    {
        public Zones[] zonesArray;

        // Server changes sync var, which auto triggers the hook on clients
        [SyncVar(hook = nameof(OnScoresChanged))]
        public int[] scoresSyncVars;

        void OnScoresChanged(int[] _old, int[] _new)
        {
            //print("OnScoresChanged");

            for (int i = 0; i < zonesArray.Length; i++)
            {
                zonesArray[i].textMesh.text = "Score: " + scoresSyncVars[i].ToString();
            }
        }

        public void UpdateScores(int _zonesID, int _score)
        {
            //print("UpdateScores, for Zone ID: " + _zonesID);

            // we need to call the sync var array this way, to trigger a change detection, so hook is called
            int[] temp = new int[scoresSyncVars.Length];
            Array.Copy(scoresSyncVars, temp, scoresSyncVars.Length);
            temp[_zonesID] += _score;
            scoresSyncVars = temp;
        }
    }
}