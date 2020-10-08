namespace Mirror
{
    public interface INetworkSceneManager
    {
        void ChangeServerScene(string newSceneName, SceneOperation sceneOperation = SceneOperation.Normal);
    }
}
