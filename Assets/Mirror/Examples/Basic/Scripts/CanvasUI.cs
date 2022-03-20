using UnityEngine;

namespace Mirror.Examples.Basic
{
    public class CanvasUI : MonoBehaviour
    {
        [Tooltip("Assign Main Panel so it can be turned on from Player:OnStartClient")]
        public RectTransform mainPanel;

        [Tooltip("Assign Players Panel for instantiating PlayerUI as child")]
        public RectTransform playersPanel;

        // static instance that can be referenced directly from Player script
        public static CanvasUI instance;

        void Awake()
        {
            instance = this;
        }
    }
}
