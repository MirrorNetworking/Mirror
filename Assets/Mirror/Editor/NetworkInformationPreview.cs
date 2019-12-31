using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Mirror
{
    [CustomPreview(typeof(GameObject))]
    class NetworkInformationPreview : ObjectPreview
    {
        class NetworkIdentityInfo
        {
            public GUIContent Name;
            public GUIContent Value;
        }

        class NetworkBehaviourInfo
        {
            // This is here just so we can check if it's enabled/disabled
            public NetworkBehaviour Behaviour;
            public GUIContent Name;
        }

        class Styles
        {
            public GUIStyle LabelStyle = new GUIStyle(EditorStyles.label);
            public GUIStyle ComponentName = new GUIStyle(EditorStyles.boldLabel);
            public GUIStyle DisabledName = new GUIStyle(EditorStyles.miniLabel);

            public Styles()
            {
                Color fontColor = new Color(0.7f, 0.7f, 0.7f);
                LabelStyle.padding.right += 20;
                LabelStyle.normal.textColor    = fontColor;
                LabelStyle.active.textColor    = fontColor;
                LabelStyle.focused.textColor   = fontColor;
                LabelStyle.hover.textColor     = fontColor;
                LabelStyle.onNormal.textColor  = fontColor;
                LabelStyle.onActive.textColor  = fontColor;
                LabelStyle.onFocused.textColor = fontColor;
                LabelStyle.onHover.textColor   = fontColor;

                ComponentName.normal.textColor = fontColor;
                ComponentName.active.textColor = fontColor;
                ComponentName.focused.textColor = fontColor;
                ComponentName.hover.textColor = fontColor;
                ComponentName.onNormal.textColor = fontColor;
                ComponentName.onActive.textColor = fontColor;
                ComponentName.onFocused.textColor = fontColor;
                ComponentName.onHover.textColor = fontColor;

                DisabledName.normal.textColor = fontColor;
                DisabledName.active.textColor = fontColor;
                DisabledName.focused.textColor = fontColor;
                DisabledName.hover.textColor = fontColor;
                DisabledName.onNormal.textColor = fontColor;
                DisabledName.onActive.textColor = fontColor;
                DisabledName.onFocused.textColor = fontColor;
                DisabledName.onHover.textColor = fontColor;
            }
        }

        List<NetworkIdentityInfo> info;
        List<NetworkBehaviourInfo> behavioursInfo;
        NetworkIdentity identity;
        GUIContent title;
        Styles styles = new Styles();

        public override void Initialize(UnityObject[] targets)
        {
            base.Initialize(targets);
            GetNetworkInformation(target as GameObject);
        }

        public override GUIContent GetPreviewTitle()
        {
            if (title == null)
            {
                title = new GUIContent("Network Information");
            }
            return title;
        }

        public override bool HasPreviewGUI()
        {
            return info != null && info.Count > 0;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            // refresh the data
            GetNetworkInformation(target as GameObject);

            if (info == null || info.Count == 0)
                return;

            if (styles == null)
                styles = new Styles();

            // Get required label size for the names of the information values we're going to show
            // There are two columns, one with label for the name of the info and the next for the value
            var maxNameLabelSize = new Vector2(140, 16);
            Vector2 maxValueLabelSize = GetMaxNameLabelSize();

            //Apply padding
            var previewPadding = new RectOffset(-5, -5, -5, -5);
            Rect paddedr = previewPadding.Add(r);

            //Centering
            float initialX = paddedr.x + 10;
            float initialY = paddedr.y + 10;

            var labelRect = new Rect(initialX, initialY, maxNameLabelSize.x, maxNameLabelSize.y);
            var idLabelRect = new Rect(maxNameLabelSize.x, initialY, maxValueLabelSize.x, maxValueLabelSize.y);

            foreach (NetworkIdentityInfo info in info)
            {
                GUI.Label(labelRect, info.Name, styles.LabelStyle);
                GUI.Label(idLabelRect, info.Value, styles.ComponentName);
                labelRect.y += labelRect.height;
                labelRect.x = initialX;
                idLabelRect.y += idLabelRect.height;
            }

            // Show behaviours list in a different way than the name/value pairs above
            float lastY = labelRect.y;
            if (behavioursInfo != null && behavioursInfo.Count > 0)
            {
                Vector2 maxBehaviourLabelSize = GetMaxBehaviourLabelSize();
                var behaviourRect = new Rect(initialX, labelRect.y + 10, maxBehaviourLabelSize.x, maxBehaviourLabelSize.y);

                GUI.Label(behaviourRect, new GUIContent("Network Behaviours"), styles.LabelStyle);
                behaviourRect.x += 20; // indent names
                behaviourRect.y += behaviourRect.height;

                foreach (NetworkBehaviourInfo info in behavioursInfo)
                {
                    if (info.Behaviour == null)
                    {
                        // could be the case in the editor after existing play mode.
                        continue;
                    }

                    GUI.Label(behaviourRect, info.Name, info.Behaviour.enabled ? styles.ComponentName : styles.DisabledName);
                    behaviourRect.y += behaviourRect.height;
                    lastY = behaviourRect.y;
                }

                if (identity.observers != null && identity.observers.Count > 0)
                {
                    var observerRect = new Rect(initialX, lastY + 10, 200, 20);

                    GUI.Label(observerRect, new GUIContent("Network observers"), styles.LabelStyle);
                    observerRect.x += 20; // indent names
                    observerRect.y += observerRect.height;

                    foreach (KeyValuePair<int, NetworkConnection> kvp in identity.observers)
                    {
                        GUI.Label(observerRect, kvp.Value.address + ":" + kvp.Value, styles.ComponentName);
                        observerRect.y += observerRect.height;
                        lastY = observerRect.y;
                    }
                }

                if (identity.connectionToClient != null)
                {
                    var ownerRect = new Rect(initialX, lastY + 10, 400, 20);
                    GUI.Label(ownerRect, new GUIContent("Client Authority: " + identity.connectionToClient), styles.LabelStyle);
                }
            }
        }

        // Get the maximum size used by the value of information items
        Vector2 GetMaxNameLabelSize()
        {
            var maxLabelSize = Vector2.zero;
            foreach (NetworkIdentityInfo info in info)
            {
                Vector2 labelSize = styles.LabelStyle.CalcSize(info.Value);
                if (maxLabelSize.x < labelSize.x)
                {
                    maxLabelSize.x = labelSize.x;
                }
                if (maxLabelSize.y < labelSize.y)
                {
                    maxLabelSize.y = labelSize.y;
                }
            }
            return maxLabelSize;
        }

        Vector2 GetMaxBehaviourLabelSize()
        {
            var maxLabelSize = Vector2.zero;
            foreach (NetworkBehaviourInfo behaviour in behavioursInfo)
            {
                Vector2 labelSize = styles.LabelStyle.CalcSize(behaviour.Name);
                if (maxLabelSize.x < labelSize.x)
                {
                    maxLabelSize.x = labelSize.x;
                }
                if (maxLabelSize.y < labelSize.y)
                {
                    maxLabelSize.y = labelSize.y;
                }
            }
            return maxLabelSize;
        }

        void GetNetworkInformation(GameObject gameObject)
        {
            identity = gameObject.GetComponent<NetworkIdentity>();
            if (identity != null)
            {
                info = new List<NetworkIdentityInfo>
                {
                    GetAssetId(),
                    GetString("Scene ID", identity.sceneId.ToString("X"))
                };

                if (!Application.isPlaying)
                {
                    return;
                }

                info.Add(GetString("Network ID", identity.netId.ToString()));

                info.Add(GetBoolean("Is Client", identity.isClient));
                info.Add(GetBoolean("Is Server", identity.isServer));
                info.Add(GetBoolean("Has Authority", identity.hasAuthority));
                info.Add(GetBoolean("Is Local Player", identity.isLocalPlayer));

                NetworkBehaviour[] behaviours = gameObject.GetComponents<NetworkBehaviour>();
                if (behaviours.Length > 0)
                {
                    behavioursInfo = new List<NetworkBehaviourInfo>();
                    foreach (NetworkBehaviour behaviour in behaviours)
                    {
                        var info = new NetworkBehaviourInfo
                        {
                            Name = new GUIContent(behaviour.GetType().FullName),
                            Behaviour = behaviour
                        };
                        behavioursInfo.Add(info);
                    }
                }
            }
        }

        NetworkIdentityInfo GetAssetId()
        {
            string assetId = identity.assetId.ToString();
            if (string.IsNullOrEmpty(assetId))
            {
                assetId = "<object has no prefab>";
            }
            return GetString("Asset ID", assetId);
        }

        static NetworkIdentityInfo GetString(string name, string value)
        {
            var info = new NetworkIdentityInfo
            {
                Name = new GUIContent(name),
                Value = new GUIContent(value)
            };
            return info;
        }

        static NetworkIdentityInfo GetBoolean(string name, bool value)
        {
            var info = new NetworkIdentityInfo
            {
                Name = new GUIContent(name),
                Value = new GUIContent((value ? "Yes" : "No"))
            };
            return info;
        }
    }
}
