using UnityEditor;
using UnityEngine.UIElements;

namespace Edgegap
{
    [CustomEditor(typeof(EdgegapToolScript))]
    public class EdgegapPluginScriptEditor : Editor
    {
        VisualElement _serverDataContainer;

        void OnEnable()
        {
            _serverDataContainer = EdgegapServerDataManager.GetServerDataVisualTree();
            EdgegapServerDataManager.RegisterServerDataContainer(_serverDataContainer);
        }

        void OnDisable()
        {
            EdgegapServerDataManager.DeregisterServerDataContainer(_serverDataContainer);
        }

        public override VisualElement CreateInspectorGUI()
        {
            return _serverDataContainer;
        }
    }
}
