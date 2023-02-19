using UnityEngine;
using UnityEngine.UI;

namespace TestNT
{
    public class LoginUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] internal InputField usernameInput;
        [SerializeField] internal Button hostButton;
        [SerializeField] internal Button clientButton;
        [SerializeField] internal Text errorText;

        public static LoginUI instance;

        void Awake()
        {
            instance = this;

#if UNITY_WEBGL
            hostButton.gameObject.SetActive(false);
#endif
        }

        // Called by UI element UsernameInput.OnValueChanged
        public void ToggleButtons(string username)
        {
#if !UNITY_WEBGL
            hostButton.interactable = !string.IsNullOrWhiteSpace(username) && string.Compare(TestNTNetworkManager.singleton.networkAddress, "localhost", true) == 0;
#endif
            clientButton.interactable = !string.IsNullOrWhiteSpace(username);
        }
    }
}
