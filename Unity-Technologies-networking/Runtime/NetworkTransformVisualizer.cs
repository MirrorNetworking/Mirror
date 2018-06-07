#if ENABLE_UNET
using System;
using System.ComponentModel;
using UnityEngine;

namespace UnityEngine.Networking
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkTransformVisualizer")]
    [RequireComponent(typeof(NetworkTransform))]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class NetworkTransformVisualizer : NetworkBehaviour
    {
        [Tooltip("The prefab to use for the visualization object.")]
        [SerializeField] GameObject m_VisualizerPrefab;

        NetworkTransform m_NetworkTransform;
        GameObject  m_Visualizer;

        public GameObject visualizerPrefab { get { return m_VisualizerPrefab; } set { m_VisualizerPrefab = value; }}

        public override void OnStartClient()
        {
            if (m_VisualizerPrefab != null)
            {
                m_NetworkTransform = GetComponent<NetworkTransform>();
                CreateLineMaterial();
                m_Visualizer = (GameObject)Instantiate(m_VisualizerPrefab, transform.position, Quaternion.identity);
            }
        }

        public override void OnStartLocalPlayer()
        {
            if (m_Visualizer == null)
                return;

            if (m_NetworkTransform.localPlayerAuthority || isServer)
            {
                Destroy(m_Visualizer);
            }
        }

        void OnDestroy()
        {
            if (m_Visualizer != null)
            {
                Destroy(m_Visualizer);
            }
        }

        [ClientCallback]
        void FixedUpdate()
        {
            if (m_Visualizer == null)
                return;

            // dont run if network isn't active
            if (!NetworkServer.active && !NetworkClient.active)
                return;

            // dont run if we haven't been spawned yet
            if (!isServer && !isClient)
                return;

            // dont run this if this client has authority over this player object
            if (hasAuthority && m_NetworkTransform.localPlayerAuthority)
                return;

            m_Visualizer.transform.position = m_NetworkTransform.targetSyncPosition;

            if (m_NetworkTransform.rigidbody3D != null && m_Visualizer.GetComponent<Rigidbody>() != null)
            {
                m_Visualizer.GetComponent<Rigidbody>().velocity = m_NetworkTransform.targetSyncVelocity;
            }
            if (m_NetworkTransform.rigidbody2D != null && m_Visualizer.GetComponent<Rigidbody2D>() != null)
            {
                m_Visualizer.GetComponent<Rigidbody2D>().velocity = m_NetworkTransform.targetSyncVelocity;
            }

            Quaternion targetFacing = Quaternion.identity;
            if (m_NetworkTransform.rigidbody3D != null)
            {
                targetFacing = m_NetworkTransform.targetSyncRotation3D;
            }
            if (m_NetworkTransform.rigidbody2D != null)
            {
                targetFacing = Quaternion.Euler(0, 0, m_NetworkTransform.targetSyncRotation2D);
            }
            m_Visualizer.transform.rotation = targetFacing;
        }

        // --------------------- local transform sync  ------------------------

        void OnRenderObject()
        {
            if (m_Visualizer == null)
                return;

            if (m_NetworkTransform.localPlayerAuthority && hasAuthority)
                return;

            if (m_NetworkTransform.lastSyncTime == 0)
                return;

            s_LineMaterial.SetPass(0);
            GL.Begin(GL.LINES);
            GL.Color(Color.white);
            GL.Vertex3(transform.position.x, transform.position.y, transform.position.z);
            GL.Vertex3(m_NetworkTransform.targetSyncPosition.x, m_NetworkTransform.targetSyncPosition.y, m_NetworkTransform.targetSyncPosition.z);
            GL.End();

            DrawRotationInterpolation();
        }

        void DrawRotationInterpolation()
        {
            Quaternion targetFacing = Quaternion.identity;
            if (m_NetworkTransform.rigidbody3D != null)
            {
                targetFacing = m_NetworkTransform.targetSyncRotation3D;
            }
            if (m_NetworkTransform.rigidbody2D != null)
            {
                targetFacing = Quaternion.Euler(0, 0, m_NetworkTransform.targetSyncRotation2D);
            }
            if (targetFacing == Quaternion.identity)
                return;

            // draw line for actual facing
            GL.Begin(GL.LINES);
            GL.Color(Color.yellow);
            GL.Vertex3(transform.position.x, transform.position.y, transform.position.z);

            Vector3 actualFront = transform.position + transform.right;
            GL.Vertex3(actualFront.x, actualFront.y, actualFront.z);
            GL.End();

            // draw line for target (server) facing
            GL.Begin(GL.LINES);
            GL.Color(Color.green);

            GL.Vertex3(transform.position.x, transform.position.y, transform.position.z);

            Vector3 targetPositionOffset = (targetFacing * Vector3.right);
            Vector3 targetFront = transform.position + targetPositionOffset;
            GL.Vertex3(targetFront.x, targetFront.y, targetFront.z);
            GL.End();
        }

        static Material s_LineMaterial;

        static void CreateLineMaterial()
        {
            if (s_LineMaterial)
                return;
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (!shader)
            {
                Debug.LogWarning("Could not find Colored builtin shader");
                return;
            }
            s_LineMaterial = new Material(shader);
            s_LineMaterial.hideFlags = HideFlags.HideAndDontSave;
            s_LineMaterial.SetInt("_ZWrite", 0);
        }
    }
}
#endif //ENABLE_UNET
