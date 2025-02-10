using System;
using Edgegap.Editor.Api.Models;

namespace Edgegap.Editor
{
    /// <summary>
    /// Contains static metadata / options for the EdgegapWindowV2 UI.
    /// - Notable:
    ///   * SHOW_DEBUG_BTN
    ///   * LOG_LEVEL
    ///   * DEFAULT_VERSION_TAG
    ///   * SKIP_SERVER_BUILD_WHEN_PUSHING
    ///   * SKIP_DOCKER_IMAGE_BUILD_WHEN_PUSHING
    /// </summary>
    public static class EdgegapWindowMetadata
    {
        #region Debug
        /// <summary>Log Debug+, or Errors only?</summary>
        public enum LogLevel
        {
            Debug,
            Error,
        }

        /// <summary>
        /// Set to Debug to show more logs. Default `Error`.
        /// - Error level includes "potentially-intentional" (!fatal) errors logged with Debug.Log
        /// - TODO: Move opt to UI?
        /// </summary>
        public const LogLevel LOG_LEVEL = LogLevel.Error;

        /// <summary>
        /// Set to show a debug button at the top-right for arbitrary testing.
        /// Default enables groups. Default `false`.
        /// </summary>
        public const bool SHOW_DEBUG_BTN = false;

        /// <summary>
        /// When running a Docker-based "Build & Push" flow, skip building the Unity server binary
        /// (great for testing push flow). Default false.
        /// </summary>
        public static readonly bool SKIP_SERVER_BUILD_WHEN_PUSHING = false; // MIRROR CHANGE: 'const' changed to 'static readonly' to avoid 'unreachable code detected' warning

        /// <summary>
        /// When running a Docker-based "Build & Push" flow, skip building the Docker image
        /// (great for testing registry login mechanics). Default false.
        /// </summary>
        public static readonly bool SKIP_DOCKER_IMAGE_BUILD_WHEN_PUSHING = false; // MIRROR CHANGE: 'const' changed to 'static readonly' to avoid 'unreachable code detected' warning
        #endregion // Debug

        /// <summary>Interval at which the server status is updated</summary>
        public const int SERVER_STATUS_CRON_JOB_INTERVAL_MS = 10000;
        public const int PORT_DEFAULT = 7770;
        public const int PORT_MIN = 1024;
        public const int PORT_MAX = 49151;
        public const int DEPLOYMENT_AWAIT_READY_STATUS_TIMEOUT_MINS = 5;
        public const int DEPLOYMENT_READY_STATUS_POLL_SECONDS = 2;
        public const int DEPLOYMENT_STOP_STATUS_POLL_SECONDS = 2;
        public const ProtocolType DEFAULT_PROTOCOL_TYPE = ProtocolType.UDP;
        public const string READY_STATUS = "Status.READY";

        public const string EDGEGAP_GET_A_TOKEN_URL = "https://app.edgegap.com/?oneClick=true";
        public const string EDGEGAP_DISCORD_URL = "https://discord.com/invite/MmJf8fWjnt";
        public const string EDGEGAP_DOC_BASE_URL = "https://docs.edgegap.com/";
        public const string EDGEGAP_DOC_PLUGIN_GUIDE_PATH =
            "learn/unity-games/developer-tools#usage-requirements";
        public const string EDGEGAP_DOC_USAGE_REQUIREMENTS_PATH =
            "learn/unity-games/developer-tools#usage-requirements";
        public const string LOCAL_TEST_CONNECT_INFO_PATH =
            "learn/unity-games/getting-started-with-servers#4-test-your-server-locally";
        public const string EDGEGAP_DOC_APP_INFO_PATH =
            "learn/unity-games/getting-started-with-servers#5-create-an-edgegap-application";
        public const string EDGEGAP_CREATE_APP_BASE_URL =
            "https://app.edgegap.com/application-management/applications/";
        public const string EDGEGAP_DOC_DEPLOY_GUIDE_PATH =
            "learn/unity-games/getting-started-with-servers#6-deploy-a-server-on-edgegap";
        public const string EDGEGAP_DEPLOY_APP_URL =
            "https://app.edgegap.com/deployment-management/deployments/list";
        public const string EDGEGAP_FREE_TIER_INFO_URL =
            "https://app.edgegap.com/user-settings?tab=memberships";
        public const string CONNECT_TO_DEPLOYMENT_INFO_URL = "docs/category/unity-netcodes";
        public const string EDGEGAP_DOC_LOBBY_PATH = "docs/lobby/service";
        public const string EDGEGAP_DOC_MANAGED_MATCHMAKER_PATH = "docs/gen2-matchmaker";
        public const string EDGEGAP_DOC_ADV_MATCHMAKER_PATH = "docs/matchmaker/advanced";
        public const string SCALING_LIFECYCLE_INFO_URL = "learn/advanced-features/deployments";

        private const string DEFAULT_UTM_SOURCE_TAG = "partner_mirror_source_unity";
        private const string DEFAULT_UTM_MEDIUM_TAG = "servers_quickstart_plugin";
        private const string DEFAULT_UTM_CONTENT_TAG = "plugin_button";
        public const string DEFAULT_UTM_TAGS =
            "utm_source="
            + DEFAULT_UTM_SOURCE_TAG
            + "&utm_medium="
            + DEFAULT_UTM_MEDIUM_TAG
            + "&utm_content="
            + DEFAULT_UTM_CONTENT_TAG;
        public const string DEFAULT_NEW_APPLICATION_LABEL = "Create New Application";
        public const string DEFAULT_DEPLOYMENT_TAG = "quickstart";
        public const string DEFAULT_VERSION_TAG = "{yy.MM.DD-HH.mm.ss}-UTC";
        public const string LOADING_RICH_STR = "Loading...";
        public const string PROCESSING_RICH_STR = "Processing...";
        public const string DEPLOY_REQUEST_RICH_STR = "Requesting Deploy...";
        public const string KEY_COMPILER_MACRO = "EDGEGAP_PLUGIN_SERVERS";

        #region Colors
        /// <summary>Earthy lime green</summary>
        public const string SUCCESS_COLOR_HEX = "#8AEE8C";

        /// <summary>Calming light orange</summary>
        public const string WARN_COLOR_HEX = "#EEC58A";

        /// <summary>Vivid blood orange</summary>
        public const string FAIL_COLOR_HEX = "#EE9A8A";

        /// <summary>Corn yellow</summary>
        public const string PROCESSING_COLOR_HEX = "#EEEA8A";

        public enum StatusColors
        {
            /// <summary>CornYellow</summary>
            Processing,

            /// <summary>EarthyLimeGreen</summary>
            Success,

            /// <summary>CalmingLightOrange</summary>
            Warn,

            /// <summary>VividBloodOrange</summary>
            Error,
        }

        /// <returns>Wraps string in color rich text</returns>
        public static string WrapRichTextInColor(string str, StatusColors statusColor)
        {
            switch (statusColor)
            {
                case StatusColors.Processing:
                    return $"<color={PROCESSING_COLOR_HEX}>{str}</color>";
                case StatusColors.Success:
                    return $"<color={SUCCESS_COLOR_HEX}>{str}</color>";
                case StatusColors.Warn:
                    return $"<color={WARN_COLOR_HEX}>{str}</color>";
                case StatusColors.Error:
                    return $"<color={FAIL_COLOR_HEX}>{str}</color>";
                default:
                    throw new ArgumentOutOfRangeException(nameof(statusColor), statusColor, null);
            }
        }
        #endregion // Colors

        #region Player Pref Key Ids for persistence
        /// <summary>Cached as base64</summary>
        public const string API_TOKEN_KEY_STR = "ApiToken";
        #endregion // Editor Pref Key Ids for persistence

        #region UI Element Ids
        //1. Connect your Edgegap account
        public const string DEBUG_BTN_ID = "DebugBtn";
        public const string API_TOKEN_TXT_ID = "ApiTokenMaskedTxt";
        public const string API_TOKEN_VERIFY_BTN_ID = "ApiTokenVerifyBtn";
        public const string API_TOKEN_GET_BTN_ID = "ApiTokenGetBtn";
        public const string SIGN_IN_CONTAINER_ID = "SignInContainer";
        public const string SIGN_IN_BTN_ID = "EdgegapSignInBtn";
        public const string CONNECTED_CONTAINER_ID = "ConnectedContainer";
        public const string SIGN_OUT_BTN_ID = "SignOutBtn";
        public const string JOIN_DISCORD_BTN_ID = "EdgegapDiscordBtn";

        public const string POST_AUTH_CONTAINER_ID = "PostAuthContainer";

        //2. Build your game server
        public const string SERVER_BUILD_FOLDOUT_ID = "BuildServerFoldout";
        public const string LINUX_REQUIREMENTS_LINK_ID = "LinuxRequirementsTxtLink";
        public const string INSTALL_LINUX_BTN_ID = "InstallLinuxRequirementsBtn";
        public const string INSTALL_LINUX_RESULT_LABEL_ID = "ValidateLinuxResultLabel";
        public const string SERVER_BUILD_PARAM_BTN_ID = "BuildParametersBtn";
        public const string SERVER_BUILD_FOLDER_TXT_ID = "BuildFolderTxt";
        public const string SERVER_BUILD_BTN_ID = "ServerBuildBtn";
        public const string SERVER_BUILD_RESULT_LABEL_ID = "ServerBuildResultLabel";

        //3. Containerize your server
        public const string CONTAINERIZE_SERVER_FOLDOUT_ID = "ContainerizeServerFoldout";
        public const string DOCKER_INSTALL_LINK_ID = "DockerRequirementTxtLink";
        public const string VALIDATE_DOCKER_INSTALL_BTN_ID = "ValidateDockerRequirementBtn";
        public const string VALIDATE_DOCKER_RESULT_LABEL_ID = "ValidateDockerResultLabel";
        public const string CONTAINERIZE_SERVER_BUILD_PATH_TXT_ID = "ContainerizeBuildPathTxt";
        public const string CONTAINERIZE_BUILD_PATH_RESET_BTN_ID = "ResetBuildPathBtn";
        public const string CONTAINERIZE_IMAGE_NAME_TXT_ID = "ContainerizeImageNameTxt";
        public const string CONTAINERIZE_IMAGE_TAG_TXT_ID = "ContainerizeImageTagTxt";
        public const string DOCKERFILE_PATH_TXT_ID = "DockerfilePathTxt";
        public const string DOCKERFILE_PATH_RESET_BTN_ID = "ResetDockerfilePathBtn";
        public const string DOCKER_BUILD_PARAMS_TXT_ID = "DockerfileParametersTxt";
        public const string CONTAINERIZE_SERVER_BTN_ID = "ContainerizeServerBtn";
        public const string CONTAINERIZE_SERVER_RESULT_LABEL_TXT = "ContainerizeResultLabel";

        //4. Test your server locally
        public const string LOCAL_TEST_FOLDOUT_ID = "LocalTestFoldout";
        public const string LOCAL_TEST_IMAGE_TXT_ID = "LocalTestImageTxt";
        public const string LOCAL_TEST_IMAGE_SHOW_DROPDOWN_BTN_ID = "LocalTestImageDropdownBtn";
        public const string LOCAL_TEST_DOCKER_RUN_TXT_ID = "DockerRunParamsTxt";
        public const string LOCAL_TEST_DEPLOY_BTN_ID = "LocalTestDeployBtn";
        public const string LOCAL_TEST_TERMINATE_BTN_ID = "LocalTestTerminateBtn";
        public const string LOCAL_TEST_DISCORD_HELP_BTN_ID = "LocalTestDiscordHelpBtn";
        public const string LOCAL_TEST_RESULT_LABEL_ID = "LocalDeployResultLabel";
        public const string LOCAL_TEST_CONNECT_LABEL_LINK_ID = "LocalContainerConnectLink";

        //5. Create an Edgegap application
        public const string CREATE_APP_FOLDOUT_ID = "EdgegapAppFoldout";
        public const string CREATE_APP_NAME_TXT_ID = "CreateAppNameTxt";
        public const string CREATE_APP_NAME_SHOW_DROPDOWN_BTN_ID = "CreateAppNameDropdownBtn";
        public const string CREATE_APP_IMAGE_NAME_TXT_ID = "ServerImageNameTxt";
        public const string CREATE_APP_IMAGE_TAG_TXT_ID = "ServerImageTagTxt";
        public const string PORT_MAPPING_LABEL_LINK_ID = "PortMappingLink";
        public const string PUSH_IMAGE_CREATE_APP_BTN_ID = "ImagePushAppCreateBtn";
        public const string EDGEGAP_APP_LABEL_LINK_ID = "EdgegapAppLink";

        //6. Deploy a server on Edgegap
        public const string DEPLOY_APP_FOLDOUT_ID = "ServerDeploymentFoldout";
        public const string DEPLOY_APP_NAME_TXT_ID = "DeployAppNameTxt";
        public const string DEPLOY_APP_NAME_SHOW_DROPDOWN_BTN_ID = "DeployAppNameDropdownBtn";
        public const string DEPLOY_APP_TAG_VERSION_TXT_ID = "DeployAppVersionTagTxt";
        public const string DEPLOY_APP_VERSION_SHOW_DROPDOWN_BTN_ID = "DeployAppVersionDropdownBtn";
        public const string DEPLOY_LIMIT_LABEL_LINK_ID = "DeployLimitLink";
        public const string DEPLOY_START_BTN_ID = "DeploymentsCreateBtn";
        public const string DEPLOY_STOP_BTN_ID = "DeploymentsConnectionServerStopBtn";
        public const string DEPLOY_DISCORD_HELP_BTN_ID = "DeployDiscordHelpBtn";
        public const string DEPLOY_RESULT_LABEL_TXT = "DeploymentResultLabel";

        //7. Matchmaking and next steps
        public const string NEXT_STEPS_FOLDOUT_ID = "NextStepFoldout";
        public const string NEXT_STEPS_SERVER_CONNECT_LINK_ID = "ServerConnectLinkTxt";
        public const string NEXT_STEPS_LOBBY_LABEL_LINK_ID = "LobbiesLinkTxt";
        public const string NEXT_STEPS_MANAGED_MATCHMAKER_LABEL_LINK_ID = "ManagedMMLinkTxt";
        public const string NEXT_STEPS_ADV_MATCHMAKER_LABEL_LINK_ID = "AdvMMLinkTxt";
        public const string NEXT_STEPS_LIFECYCLE_LABEL_LINK_ID = "LifecycleManageTxt";
        #endregion // UI Element Ids



        //[Obsolete("Hard-coded; not from UI. TODO: Get from UI")] // MIRROR CHANGE: comment this out to avoid import warnings
        public const ApiEnvironment API_ENVIRONMENT = ApiEnvironment.Console;
    }
}
