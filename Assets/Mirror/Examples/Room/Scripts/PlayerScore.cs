using UnityEngine;

namespace Mirror.Examples.NetworkRoom
{
    public class PlayerScore : NetworkBehaviour
    {
        [SyncVar]
        [SerializeField] internal int index;

        [SyncVar]
        [SerializeField] internal uint score;

        void OnGUI()
        {
            GUI.Box(new Rect(10f + (index * 110), 10f, 100f, 25f), $"P{index}: {score.ToString("0000000")}");
        }
    }
}
