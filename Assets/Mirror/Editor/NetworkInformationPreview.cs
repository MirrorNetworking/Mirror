using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomPreview(typeof(GameObject))]
    class NetworkInformationPreview : ObjectPreview
    {
        struct NetworkIdentityInfo
        {
            public GUIContent Name;
            public GUIContent Value;
        }

        struct NetworkBehaviourInfo
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
                var fontColor = new Color(0.7f, 0.7f, 0.7f);
                LabelStyle.padding.right += 20;
                LabelStyle.normal.textColor = fontColor;
                LabelStyle.active.textColor = fontColor;
                LabelStyle.focused.textColor = fontColor;
                LabelStyle.hover.textColor = fontColor;
                LabelStyle.onNormal.textColor = fontColor;
                LabelStyle.onActive.textColor = fontColor;
                LabelStyle.onFocused.textColor = fontColor;
                LabelStyle.onHover.textColor = fontColor;

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

        GUIContent title;
        Styles styles = new Styles();

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
            // need to check if target is null to stop MissingReferenceException 
            return target != null && target is GameObject gameObject && gameObject.GetComponent<NetworkIdentity>() != null;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (target == null)
                return;

            var targetGameObject = target as GameObject;

            if (targetGameObject == null)
                return;

            NetworkIdentity identity = targetGameObject.GetComponent<NetworkIdentity>();

            if (identity == null)
                return;

            if (styles == null)
                styles = new Styles();

            // padding
            var previewPadding = new RectOffset(-5, -5, -5, -5);
            Rect paddedr = previewPadding.Add(r);

            //Centering
            float initialX = paddedr.x + 10;
            float Y = paddedr.y + 10;

            Y = DrawNetworkIdentityInfo(identity, initialX, Y);

            Y = DrawNetworkBehaviors(identity, initialX, Y);

            Y = DrawObservers(identity, initialX, Y);

            _ = DrawOwner(identity, initialX, Y);

        }

        float DrawNetworkIdentityInfo(NetworkIdentity identity, float initialX, float Y)
        {
            IEnumerable<NetworkIdentityInfo> infos = GetNetworkIdentityInfo(identity);
            // Get required label size for the names of the information values we're going to show
            // There are two columns, one with label for the name of the info and the next for the value
            var maxNameLabelSize = new Vector2(140, 16);
            Vector2 maxValueLabelSize = GetMaxNameLabelSize(infos);

            var labelRect = new Rect(initialX, Y, maxNameLabelSize.x, maxNameLabelSize.y);
            var idLabelRect = new Rect(maxNameLabelSize.x, Y, maxValueLabelSize.x, maxValueLabelSize.y);

            foreach (NetworkIdentityInfo info in infos)
            {
                GUI.Label(labelRect, info.Name, styles.LabelStyle);
                GUI.Label(idLabelRect, info.Value, styles.ComponentName);
                labelRect.y += labelRect.height;
                labelRect.x = initialX;
                idLabelRect.y += idLabelRect.height;
            }

            return labelRect.y;
        }

        float DrawNetworkBehaviors(NetworkIdentity identity, float initialX, float Y)
        {
            IEnumerable<NetworkBehaviourInfo> behavioursInfo = GetNetworkBehaviorInfo(identity);

            // Show behaviours list in a different way than the name/value pairs above

            Vector2 maxBehaviourLabelSize = GetMaxBehaviourLabelSize(behavioursInfo);
            var behaviourRect = new Rect(initialX, Y + 10, maxBehaviourLabelSize.x, maxBehaviourLabelSize.y);

            GUI.Label(behaviourRect, new GUIContent("Network Behaviours"), styles.LabelStyle);
            // indent names
            behaviourRect.x += 20;
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
                Y = behaviourRect.y;
            }

            return Y;
        }

        float DrawObservers(NetworkIdentity identity, float initialX, float Y)
        {
            if (identity.observers.Count > 0)
            {
                var observerRect = new Rect(initialX, Y + 10, 200, 20);

                GUI.Label(observerRect, new GUIContent("Network observers"), styles.LabelStyle);
                // indent names
                observerRect.x += 20;
                observerRect.y += observerRect.height;

                foreach (INetworkConnection conn in identity.observers)
                {

                    GUI.Label(observerRect, conn.Address + ":" + conn, styles.ComponentName);
                    observerRect.y += observerRect.height;
                    Y = observerRect.y;
                }
            }

            return Y;
        }

        float DrawOwner(NetworkIdentity identity, float initialX, float Y)
        {
            if (identity.ConnectionToClient != null)
            {
                var ownerRect = new Rect(initialX, Y + 10, 400, 20);
                GUI.Label(ownerRect, new GUIContent("Client Authority: " + identity.ConnectionToClient), styles.LabelStyle);
                Y += ownerRect.height;
            }
            return Y;
        }

        // Get the maximum size used by the value of information items
        Vector2 GetMaxNameLabelSize(IEnumerable<NetworkIdentityInfo> infos)
        {
            Vector2 maxLabelSize = Vector2.zero;
            foreach (NetworkIdentityInfo info in infos)
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

        Vector2 GetMaxBehaviourLabelSize(IEnumerable<NetworkBehaviourInfo> behavioursInfo)
        {
            Vector2 maxLabelSize = Vector2.zero;
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

        IEnumerable<NetworkIdentityInfo> GetNetworkIdentityInfo(NetworkIdentity identity)
        {
            var infos = new List<NetworkIdentityInfo>
            {
                GetAssetId(identity),
                GetString("Scene ID", identity.sceneId.ToString("X"))
            };

            if (Application.isPlaying)
            {
                infos.Add(GetString("Network ID", identity.NetId.ToString()));
                infos.Add(GetBoolean("Is Client", identity.IsClient));
                infos.Add(GetBoolean("Is Server", identity.IsServer));
                infos.Add(GetBoolean("Has Authority", identity.HasAuthority));
                infos.Add(GetBoolean("Is Local Player", identity.IsLocalPlayer));
            }
            return infos;
        }

        IEnumerable<NetworkBehaviourInfo> GetNetworkBehaviorInfo(NetworkIdentity identity)
        {
            var behaviourInfos = new List<NetworkBehaviourInfo>();

            NetworkBehaviour[] behaviours = identity.GetComponents<NetworkBehaviour>();
            foreach (NetworkBehaviour behaviour in behaviours)
            {
                behaviourInfos.Add(new NetworkBehaviourInfo
                {
                    Name = new GUIContent(behaviour.GetType().FullName),
                    Behaviour = behaviour
                });
            }
            return behaviourInfos;
        }

        NetworkIdentityInfo GetAssetId(NetworkIdentity identity)
        {
            string assetId = identity.AssetId.ToString();
            if (string.IsNullOrEmpty(assetId))
            {
                assetId = "<object has no prefab>";
            }
            return GetString("Asset ID", assetId);
        }

        static NetworkIdentityInfo GetString(string name, string value)
        {
            return new NetworkIdentityInfo
            {
                Name = new GUIContent(name),
                Value = new GUIContent(value)
            };
        }

        static NetworkIdentityInfo GetBoolean(string name, bool value)
        {
            return new NetworkIdentityInfo
            {
                Name = new GUIContent(name),
                Value = new GUIContent(value ? "Yes" : "No")
            };
        }
    }
}
