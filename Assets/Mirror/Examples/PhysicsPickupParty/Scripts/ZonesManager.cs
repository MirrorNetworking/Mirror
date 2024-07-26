using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System;

namespace Mirror.Examples.PhysicsPickupParty
{
    public class ZonesManager : NetworkBehaviour
    {
        public SceneReference sceneReference;

        public Zones[] zonesArray;

        // Server changes sync var, which auto triggers the hook on clients
        [SyncVar(hook = nameof(OnScoresChanged))]
        public int[] scoresSyncVars;

        private List<int> zoneResultsList = new List<int>();
        public List<Tuple<int, int>> zoneResultsListTuple = new List<Tuple<int, int>>();

        public override void OnStartServer()
        {
            foreach (Zones zone in zonesArray)
            {
                zone.enabled = true;
            }
        }

        void OnScoresChanged(int[] _old, int[] _new)
        {
            //print("OnScoresChanged");

            for (int i = 0; i < zonesArray.Length; i++)
            {
                zonesArray[i].textMesh.text = "Score: " + scoresSyncVars[i].ToString();
            }
            CalculateZoneWinnersList();
        }

        public void UpdateScores(int _zonesID, int _score)
        {
            //print("UpdateScores, for Zone ID: " + _zonesID);

            // we need to call the sync var array this way, to trigger a change detection, so hook is called
            int[] temp = new int[scoresSyncVars.Length];
            Array.Copy(scoresSyncVars, temp, scoresSyncVars.Length);
            temp[_zonesID] += _score;
            scoresSyncVars = temp;

            if (sceneReference.playerPickupParty.teamID == _zonesID)
            {
                RpcPlayAudio();
            }
        }

        public void CalculateZoneWinnersList()
        {
            zoneResultsList = new List<int>(scoresSyncVars);
            zoneResultsListTuple.Clear();

            for (int i = 0; i < zoneResultsList.Count; i++)
            {
                zoneResultsListTuple.Add(new Tuple<int, int>(zoneResultsList[i], i));
                //print($"Value: {zoneResultsListTuple[i]}");
            }
            zoneResultsListTuple.Sort((a, b) => b.Item1.CompareTo(a.Item1));

            //print("1st winner is teamID: " + zoneResultsListTuple[0].Item2 + " - with score of: "  + zoneResultsListTuple[0].Item1);
            //print("2nd winner is teamID: " + zoneResultsListTuple[1].Item2 + " - with score of: " + zoneResultsListTuple[1].Item1);
            //print("3rd winner is teamID: " + zoneResultsListTuple[2].Item2 + " - with score of: " + zoneResultsListTuple[2].Item1);
            //print("4th winner is teamID: " + zoneResultsListTuple[3].Item2 + " - with score of: " + zoneResultsListTuple[3].Item1);

            sceneReference.UpdateScoresUI(0,zoneResultsListTuple[0].Item2, zoneResultsListTuple[0].Item1);
            sceneReference.UpdateScoresUI(1,zoneResultsListTuple[1].Item2, zoneResultsListTuple[1].Item1);
            sceneReference.UpdateScoresUI(2,zoneResultsListTuple[2].Item2, zoneResultsListTuple[2].Item1);
            sceneReference.UpdateScoresUI(3,zoneResultsListTuple[3].Item2, zoneResultsListTuple[3].Item1);
        }

        [ClientRpc]
        public void RpcPlayAudio()
        {
            PlayAudio();
        }

        public void PlayAudio()
        {
            sceneReference.scoreSound.Play();
        }
    }
}