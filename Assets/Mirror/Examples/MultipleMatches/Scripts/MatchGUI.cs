using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.MultipleMatch
{
    public class MatchGUI : MonoBehaviour
    {
        Guid matchId;

        [Header("GUI Elements")]
        public Image image;
        public Toggle toggleButton;
        public Text matchName;
        public Text playerCount;

        [Header("Diagnostics - Do Not Modify")]
        public CanvasController canvasController;

        public void Awake()
        {
#if UNITY_2021_3_OR_NEWER
            canvasController = GameObject.FindAnyObjectByType<CanvasController>();
#else
            // Deprecated in Unity 2023.1
            canvasController = GameObject.FindObjectOfType<CanvasController>();
#endif
            toggleButton.onValueChanged.AddListener(delegate { OnToggleClicked(); });
        }

        [ClientCallback]
        public void OnToggleClicked()
        {
            canvasController.SelectMatch(toggleButton.isOn ? matchId : Guid.Empty);
            image.color = toggleButton.isOn ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 1f, 1f, 0.2f);
        }

        [ClientCallback]
        public Guid GetMatchId() => matchId;

        [ClientCallback]
        public void SetMatchInfo(MatchInfo infos)
        {
            matchId = infos.matchId;
            matchName.text = $"Match {infos.matchId.ToString().Substring(0, 8)}";
            playerCount.text = $"{infos.players} / {infos.maxPlayers}";
        }
    }
}
