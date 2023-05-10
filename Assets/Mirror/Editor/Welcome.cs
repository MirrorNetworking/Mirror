// Shows either a welcome message, only once per session.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    static class Welcome
    {
        [InitializeOnLoadMethod]
        static void OnInitializeOnLoad()
        {
            // InitializeOnLoad is called on start and after each rebuild,
            // but we only want to show this once per editor session.
            if (!SessionState.GetBool("MIRROR_WELCOME", false))
            {
                SessionState.SetBool("MIRROR_WELCOME", true);
                Debug.Log("Mirror | mirror-networking.com | discord.gg/N9QVxbM");
            }
        }
    }
}
#endif
