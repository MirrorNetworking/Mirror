using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HeadlessManager : MonoBehaviour
{
    // a script to manager the handling of headless builds, either server or client
    // as server cannot press UI, we have to automate the process between scenes (if NetworkManager scene is not first in build settings)
    public string sceneNameToLoad = "MirrorShooter";

#if UNITY_SERVER && !UNITY_EDITOR
    void Start()
    {
        if (sceneNameToLoad != "")
        {
            SceneManager.LoadScene(sceneNameToLoad);
        }
    }
#endif
}
