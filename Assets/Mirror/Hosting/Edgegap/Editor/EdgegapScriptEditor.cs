#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;
using Edgegap;

[CustomEditor(typeof(EdgegapToolScript))]
public class EdgegapPluginScriptEditor : Editor
{
    VisualElement _serverDataContainer;

    private void OnEnable()
    {
        _serverDataContainer = EdgegapServerDataManager.GetServerDataVisualTree();
        EdgegapServerDataManager.RegisterServerDataContainer(_serverDataContainer);
    }

    private void OnDisable()
    {
        EdgegapServerDataManager.DeregisterServerDataContainer(_serverDataContainer);
    }

    public override VisualElement CreateInspectorGUI()
    {
        return _serverDataContainer;
    }
}
#endif