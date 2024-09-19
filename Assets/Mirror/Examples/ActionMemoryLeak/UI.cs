using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI : MonoBehaviour
{
    private void OnGUI()
    {

        GUILayout.BeginArea(new Rect(20, 120, 215, 300));
        if (GUILayout.Button("GC.Collect"))
        {
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
        double use = GC.GetTotalMemory(false) / 1024.0 / 1024;
        GUILayout.TextArea($"{use:N1} MiB");
        GUILayout.EndArea();
    }
}
