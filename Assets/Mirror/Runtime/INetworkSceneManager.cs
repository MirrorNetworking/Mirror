namespace Mirror
{
    public interface IClientSceneManager
    {
        void SetClientReady();
    }

    public interface INetworkSceneManager : IClientSceneManager
    {
        void ChangeServerScene(string newSceneName, SceneOperation sceneOperation = SceneOperation.Normal);
    }
}
