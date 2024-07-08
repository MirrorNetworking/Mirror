using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.PhysicsPickupParty
{
    public class SceneReference : MonoBehaviour
    {
        // We will use a non-networked object/script, to hold all our references to other scripts, objects and UI.
        // As networked objects may be disabled in certain situations.


        public TeamManager teamManager;
        public Text gameStartTimer, roundEndTimer;
        public Image[] UIBackgrounds;
        public GameObject panelControls, panelInfo, panelGameStartTimer, panelRoundEndTimer;

        public void SetUIBGTeamColour(int _team)
        {
            print("SetUIBGTeamColour: " + _team);
            foreach (Image item in UIBackgrounds)
            {
                item.color = teamManager.teamColours[_team];
            }
        }
    }
}