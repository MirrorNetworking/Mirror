using UnityEngine;

namespace Mirror.Examples
{
    /// <summary>
    /// Run In Background. Attach this as a component anywhere in the scene if you want the build to run in background.
    /// </summary>
    public class RunInBackground : MonoBehaviour
    {
        void Start()
        {
            Application.runInBackground = true;
        }
    }
}
