#if ENABLE_UNET
using System;
using UnityEngine;
using UnityEngine.Networking;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.Networking
{
    [CustomPreview(typeof(GameObject))]
    class NetworkTransformPreview : ObjectPreview
    {
        NetworkTransform m_Transform;
        Rigidbody m_Rigidbody3D;
        Rigidbody2D m_Rigidbody2D;

        GUIContent m_Title;

        public override void Initialize(UnityObject[] targets)
        {
            base.Initialize(targets);
            GetNetworkInformation(target as GameObject);
        }

        public override GUIContent GetPreviewTitle()
        {
            if (m_Title == null)
            {
                m_Title = new GUIContent("Network Transform");
            }
            return m_Title;
        }

        public override bool HasPreviewGUI()
        {
            return m_Transform != null;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (m_Transform == null)
                return;

            const int padding = 4;
            int posY = 4;

            float posDiff = (m_Transform.transform.position - m_Transform.targetSyncPosition).magnitude;
            GUI.Label(new Rect(r.xMin + padding, r.y + posY, 600, 20), "Position: " + m_Transform.transform.position + " Target: " +  m_Transform.targetSyncPosition + " Diff: " + posDiff);
            posY += 20;

            if (m_Rigidbody3D != null)
            {
                float angleDiff3D = Quaternion.Angle(m_Transform.rigidbody3D.rotation, m_Transform.targetSyncRotation3D);
                GUI.Label(new Rect(r.xMin + padding, r.y + posY, 600, 20), "Angle: " + m_Transform.rigidbody3D.rotation + " Target: " + m_Transform.targetSyncRotation3D + " Diff: " + angleDiff3D);
                posY += 20;

                GUI.Label(new Rect(r.xMin + padding, r.y + posY, 600, 20), "Velocity: " + m_Transform.rigidbody3D.velocity + " Target: " + m_Transform.targetSyncVelocity);
                posY += 20;
            }

            if (m_Rigidbody2D != null)
            {
                float angleDiff2D = m_Transform.rigidbody2D.rotation - m_Transform.targetSyncRotation2D;
                GUI.Label(new Rect(r.xMin + padding, r.y + posY, 600, 20), "Angle: " + m_Transform.rigidbody2D.rotation + " Target: " + m_Transform.targetSyncRotation2D + " Diff: " + angleDiff2D);
                posY += 20;

                GUI.Label(new Rect(r.xMin + padding, r.y + posY, 600, 20), "Velocity: " + m_Transform.rigidbody2D.velocity + " Target: " + m_Transform.targetSyncVelocity);
                posY += 20;
            }

            GUI.Label(new Rect(r.xMin + padding, r.y + posY, 200, 20), "Last SyncTime: " + (Time.time - m_Transform.lastSyncTime));
            posY += 20;
        }

        void GetNetworkInformation(GameObject gameObject)
        {
            m_Transform = gameObject.GetComponent<NetworkTransform>();

            m_Rigidbody3D = gameObject.GetComponent<Rigidbody>();
            m_Rigidbody2D = gameObject.GetComponent<Rigidbody2D>();
        }
    }
}
#endif //ENABLE_UNET
