using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

namespace Mirror.Examples.Shooter
{
    public class PlayerScores : NetworkBehaviour
    {
        [Serializable]
        public struct StructScores
        {
            public string scoresName;
            public int scoresKills;
            public int scoresDeaths;
            public uint scoresNetId;
        }

        readonly SyncList<StructScores> ScoresList = new SyncList<StructScores>();
        private StructScores newScore;
        private bool playerExistsInScores = false;

        public Button buttonOpen, buttonClose;
        public GameObject playerScores;
        public Text namesSection, killsSection, deathsSection;

        private void Start()
        {
            buttonOpen.onClick.AddListener(ButtonOpen);
            buttonClose.onClick.AddListener(ButtonClose);

            ScoresList.Callback += OnScoresUpdated;
            UpdateUI();
        }

        public void ButtonOpen()
        {
            playerScores.SetActive(true);
            buttonOpen.gameObject.SetActive(false);
        }

        public void ButtonClose()
        {
            playerScores.SetActive(false);
            buttonOpen.gameObject.SetActive(true);
        }

        void OnScoresUpdated(SyncList<StructScores>.Operation op, int index, StructScores oldItem, StructScores newItem)
        {
            //print("OnSyncListUpdated: " + op);
            switch (op)
            {
                case SyncList<StructScores>.Operation.OP_ADD:
                    // index is where it got added in the list
                    // item is the new item
                    break;
                case SyncList<StructScores>.Operation.OP_CLEAR:
                    // list got cleared
                    //Debug.Log("Clear");
                    break;
                case SyncList<StructScores>.Operation.OP_INSERT:
                    // index is where it got added in the list
                    // item is the new item
                    break;
                case SyncList<StructScores>.Operation.OP_REMOVEAT:
                    // index is where it got removed in the list
                    // item is the item that was removed
                    break;
                case SyncList<StructScores>.Operation.OP_SET:
                    // index is the index of the item that was updated
                    // item is the previous item
                    break;
            }
            UpdateUI();
        }

        void UpdateUI()
        {
            //Debug.Log("UpdateUI");

            namesSection.text = "";
            for (int i = 0; i < ScoresList.Count; i++)
            {
                namesSection.text += ScoresList[i].scoresName + "\n";
            }

            killsSection.text = "";
            for (int i = 0; i < ScoresList.Count; i++)
            {
                killsSection.text += ScoresList[i].scoresKills + "\n";
            }

            deathsSection.text = "";
            for (int i = 0; i < ScoresList.Count; i++)
            {
                deathsSection.text += ScoresList[i].scoresDeaths + "\n";
            }
        }

        public override void OnStartServer()
        {
            //Debug.Log("Server started");
            // for testing, fills in scores list
            //for (int i = 0; i < 30; i++)
            //{
            //    newScore = new StructScores { scoresName = "Player: " + UnityEngine.Random.Range(0, 1000), scoresKills = UnityEngine.Random.Range(0, 1000), scoresDeaths = UnityEngine.Random.Range(0, 1000), scoresNetId = (ushort)UnityEngine.Random.Range(100, 1000) };
            //    ScoresList.Add(newScore);
            //}
        }

        void SetScores(int _index, StructScores _value)
        {
           // Debug.Log("SetScores");
            ScoresList[_index] = new StructScores { scoresName = _value.scoresName, scoresKills = _value.scoresKills, scoresDeaths = _value.scoresDeaths, scoresNetId = _value.scoresNetId };
        }

        public void UpdateScore(string _scoresName, int _scoresKills, int _scoresDeaths, uint _scoresNetId)
        {
            playerExistsInScores = false;

            newScore = new StructScores { scoresName = _scoresName, scoresKills = _scoresKills, scoresDeaths = _scoresDeaths, scoresNetId = _scoresNetId };

            for (int i = 0; i < ScoresList.Count; i++)
            {
                if (ScoresList[i].scoresNetId == newScore.scoresNetId)
                {
                    SetScores(i, newScore);
                    playerExistsInScores = true;
                    //Debug.Log("Updating current score: " + _scoresName + " : " + _scoresKills + " : " + _scoresDeaths + " : " + _scoresNetId);
                }
            }

            if (playerExistsInScores == false)
            {
                ScoresList.Add(newScore);
                //Debug.Log("Adding new score: " + _scoresName + " : " + _scoresKills + " : " + _scoresDeaths + " : " + _scoresNetId);
            }
        }
    }
}