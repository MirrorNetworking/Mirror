using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.MultipleMatch
{
    public class RoomGUI : MonoBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(RoomGUI));

        public GameObject playerList;
        public GameObject playerPrefab;
        public GameObject cancelButton;
        public GameObject leaveButton;
        public Button startButton;
        public bool owner;

        public void RefreshRoomPlayers(PlayerInfo[] playerInfos)
        {
            if (logger.LogEnabled()) logger.Log($"RefreshRoomPlayers: {playerInfos.Length} playerInfos");

            foreach (Transform child in playerList.transform)
            {
                Destroy(child.gameObject);
            }

            startButton.interactable = false;
            bool everyoneReady = true;

            foreach (PlayerInfo playerInfo in playerInfos)
            {
                GameObject newPlayer = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
                newPlayer.transform.SetParent(playerList.transform, false);
                newPlayer.GetComponent<PlayerGUI>().SetPlayerInfo(playerInfo);
                if (!playerInfo.ready)
                {
                    everyoneReady = false;
                }
            }

            startButton.interactable = everyoneReady && owner && (playerInfos.Length > 1);
        }

        public void SetOwner(bool owner)
        {
            this.owner = owner;
            cancelButton.SetActive(owner);
            leaveButton.SetActive(!owner);
        }
    }
}