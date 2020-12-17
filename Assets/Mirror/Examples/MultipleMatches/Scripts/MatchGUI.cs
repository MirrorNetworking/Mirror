using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.MultipleMatch
{
    public class MatchGUI : MonoBehaviour
    {
        Guid matchId;

        [Header("GUI Elements")]
        [SerializeField] Image image;
        [SerializeField] Toggle toggleButton;
        [SerializeField] Text matchName;
        [SerializeField] Text playerCount;

        [Header("Diagnostics - Do Not Modify")]
        [SerializeField] internal CanvasController canvasController;

        public void Awake()
        {
            canvasController = FindObjectOfType<CanvasController>();
            toggleButton.onValueChanged.AddListener(delegate { OnToggleClicked(); });
        }

        public void OnToggleClicked()
        {
            canvasController.SelectMatch(toggleButton.isOn ? matchId : Guid.Empty);
            image.color = toggleButton.isOn ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 1f, 1f, 0.2f);
        }

        public Guid GetMatchId()
        {
            return matchId;
        }

        public void SetMatchInfo(MatchInfo infos)
        {
            matchId = infos.matchId;
            matchName.text = "Match " + infos.matchId.ToString().Substring(0, 8);
            playerCount.text = infos.players + " / " + infos.maxPlayers;
        }
    }
}
