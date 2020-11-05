using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Component that will display the clients ping in milliseconds
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkPingDisplay")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkPingDisplay.html")]
    public class NetworkPingDisplay : MonoBehaviour
    {
        [SerializeField] bool showPing = true;
        [SerializeField] Vector2 position = new Vector2(200, 0);
        [SerializeField] int fontSize = 24;
        [SerializeField] Color textColor = new Color32(255, 255, 255, 80);
        [SerializeField] string textPrefix;
        [SerializeField] string textSuffix;

        GUIStyle style;

        void Awake()
        {
            style = new GUIStyle();
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = fontSize;
            style.normal.textColor = textColor;
        }

        public void UpdateShowPing(bool newShowPing)
        {
            showPing = newShowPing;
        }

        public void UpdatePosition(Vector2 newTextPosition)
        {
            position = newTextPosition;
        }

        public void UpdateFontSize(int newFontSize)
        {
            fontSize = newFontSize;
            style.fontSize = fontSize;
        }

        public void UpdateTextColor(Color newTextColor)
        {
            textColor = newTextColor;
            style.normal.textColor = textColor;
        }

        public void UpdateTextPrefix(string newTextPrefix)
        {
            textPrefix = newTextPrefix;
        }

        public void UpdateTextSuffix(string newTextSuffix)
        {
            textSuffix = newTextSuffix;
        }

        void OnGUI()
        {
            if (!showPing) { return; }

            string text = string.Format("{0}ms", (int)(NetworkTime.rtt * 1000));

            int width = Screen.width;
            int height = Screen.height;
            Rect rect = new Rect(position.x, position.y, width - 200, height * 2 / 100);

            GUI.Label(rect,textPrefix + text + textSuffix, style);
        }
    }
}
