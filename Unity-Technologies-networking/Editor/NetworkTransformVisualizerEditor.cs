using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityEditor
{
    [CustomEditor(typeof(NetworkTransformVisualizer), true)]
    [CanEditMultipleObjects]
    public class NetworkTransformVisualizerEditor : NetworkBehaviourInspector
    {
        internal override bool hideScriptField
        {
            get
            {
                return true;
            }
        }
    }
}
