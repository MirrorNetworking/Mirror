namespace Edgegap
{
    public enum ToolState
    {
        Disconnected,
        Connecting,
        Connected, // Waiting for a deployment
        Building,
        Pushing,
        ProcessingDeployment,
        DeploymentRunning,
    }

    public static class PluginStateExtensions
    {
        public static bool CanConnect(this ToolState currentState)
        {
            return currentState == ToolState.Disconnected;
        }

        public static bool CanDisconnect(this ToolState currentState)
        {
            return currentState == ToolState.Connected;
        }

        public static bool CanStartDeployment(this ToolState currentState)
        {
            return currentState == ToolState.Connected;
        }

        public static bool CanStopDeployment(this ToolState currentState)
        {
            return currentState == ToolState.DeploymentRunning;
        }

        public static bool CanEditConnectionInfo(this ToolState currentState)
        {
            return currentState.CanConnect();
        }

        public static bool HasActiveDeployment(this ToolState currentState)
        {
            return currentState == ToolState.ProcessingDeployment || currentState == ToolState.DeploymentRunning;
        }
    }
}