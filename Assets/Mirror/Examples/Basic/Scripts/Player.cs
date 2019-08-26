using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.Basic2
{
    public class Player : NetworkBehaviour
    {
        [Header("Player Components")]
        public RectTransform rectTransform;
        public Image image;

        [Header("Child Text Objects")]
        public Text playerNameText;
        public Text playerDataText;

        [SyncVar]
        int playerNo;
        [SyncVar]
        int playerData;
        [SyncVar]
        Color playerColor;

        public override void OnStartServer()
        {
            base.OnStartServer();
            playerNo = connectionToClient.connectionId;
            playerColor = Random.ColorHSV(0f, 1f, 0.9f, 0.9f, 1f, 1f);
            InvokeRepeating(nameof(UpdateData), 1, 1);
        }

        void UpdateData()
        {
            playerData = Random.Range(100, 1000);
        }

        void Start()
        {
            transform.SetParent(GameObject.Find("PlayersPanel").transform);

            int x = 100 + ((playerNo % 4) * 150);
            int y = -170 - ((playerNo / 4) * 80);
            rectTransform.anchoredPosition = new Vector2(x, y);
        }

        void Update()
        {
            // shade the panel background for the local player
            if (isLocalPlayer)
                image.color = new Color(1f, 1f, 1f, 0.1f);

            playerNameText.color = playerColor;
            playerNameText.text = string.Format("Player {0:00}", playerNo);
            playerDataText.text = string.Format("Data: {0:000}", playerData);
        }
    }
}
