using UnityEngine;

namespace Mirror.Examples.MultipleAdditiveScenes
{
    public class PlayerScore : NetworkBehaviour
    {
        [SyncVar]
        [SerializeField] internal int playerNumber;

        [SyncVar]
        [SerializeField] internal int scoreIndex;

        [SyncVar]
        [SerializeField] internal int matchIndex;

        [SyncVar]
        [SerializeField] internal uint score;

        [SerializeField] int clientMatchIndex = -1;

        void OnGUI()
        {
            if (!isServerOnly && !isLocalPlayer && clientMatchIndex < 0)
                clientMatchIndex = NetworkClient.connection.identity.GetComponent<PlayerScore>().matchIndex;

            if (isLocalPlayer || matchIndex == clientMatchIndex)
                GUI.Box(new Rect(10f + (scoreIndex * 110), 10f, 100f, 25f), $"P{playerNumber}: {score}");
        }
    }
}
