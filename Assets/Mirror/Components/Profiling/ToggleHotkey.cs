using UnityEngine;
namespace Mirror
{
    public class ToggleHotkey : MonoBehaviour
    {
        public KeyCode Key = KeyCode.F10;
        public GameObject ToToggle;

        void Update()
        {
            if (Input.GetKeyDown(Key))
                ToToggle.SetActive(!ToToggle.activeSelf);
        }
    }
}
