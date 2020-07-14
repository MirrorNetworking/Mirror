namespace Mirror
{
    public interface INetworkSceneManager
    {
        void ChangeServerScene(string newSceneName, SceneOperation sceneOperation = SceneOperation.Normal);

        void SetClientReady(INetworkConnection conn);
    }
}
