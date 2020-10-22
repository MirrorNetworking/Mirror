namespace Mirror
{
    public interface INetworkSceneManager
    {
        void ChangeServerScene(string scenePath, SceneOperation sceneOperation = SceneOperation.Normal);
    }
}
