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
        public const int DEPLOYMENT_AWAIT_READY_STATUS_TIMEOUT_MINS = 1;
        public const int DEPLOYMENT_READY_STATUS_POLL_SECONDS = 2;
        public const int DEPLOYMENT_STOP_STATUS_POLL_SECONDS = 2;
        public const ProtocolType DEFAULT_PROTOCOL_TYPE = ProtocolType.UDP;
        public const string READY_STATUS = "Status.READY";

        public const string EDGEGAP_GET_A_TOKEN_URL = "https://app.edgegap.com/?oneClick=true";
        public const string EDGEGAP_ADD_MORE_GAME_SERVERS_URL = "https://edgegap.com/resources/contact";
        public const string EDGEGAP_DOC_BTN_HOW_TO_LOGIN_VIA_CLI_URL = "https://docs.edgegap.com/docs/container/edgegap-container-registry/#getting-your-credentials";
        private const string DEFAULT_UTM_SOURCE_TAG = "partner_mirror_assetstore_unity";
        private const string DEFAULT_UTM_MEDIUM_TAG = "servers_quickstart_plugin";
        private const string DEFAULT_UTM_CONTENT_TAG = "plugin_button";
        public const string DEFAULT_UTM_TAGS = "utm_source=" + DEFAULT_UTM_SOURCE_TAG +
                                               "&utm_medium=" + DEFAULT_UTM_MEDIUM_TAG +
                                               "&utm_content=" + DEFAULT_UTM_CONTENT_TAG;
        public const string DEFAULT_VERSION_TAG = "latest";
        public const string LOADING_RICH_STR = "<i>Loading...</i>";
        public const string PROCESSING_RICH_STR = "<i>Processing...</i>";
        public const string DEPLOY_REQUEST_RICH_STR = "<i>Requesting Deploy...</i>";
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
        public const string DEPLOYMENT_REQUEST_ID_KEY_STR = "DeploymentRequestId";
        public const string DEPLOYMENT_CONNECTION_URL_KEY_STR = "DeploymentConnectionUrlLabel";
        public const string DEPLOYMENT_CONNECTION_STATUS_KEY_STR = "DeploymentsConnectionStatusLabel";
        public const string CONTAINER_REGISTRY_TRANSPORT_TYPE_ENUM_KEY_STR = "ContainerRegistryProtocolTypeEnum";
        #endregion // Editor Pref Key Ids for persistence

        #region UI Element Ids
        public const string DEBUG_BTN_ID = "DebugBtn";
        public const string API_TOKEN_TXT_ID = "ApiTokenMaskedTxt";
        public const string API_TOKEN_VERIFY_BTN_ID = "ApiTokenVerifyPurpleBtn";
        public const string API_TOKEN_GET_BTN_ID = "ApiTokenGetBtn";
        public const string POST_AUTH_CONTAINER_ID = "PostAuthContainer";

        public const string APP_INFO_FOLDOUT_ID = "ApplicationInfoFoldout";
        public const string APP_NAME_TXT_ID = "ApplicationNameTxt";
        public const string APP_LOAD_EXISTING_BTN_ID = "AppLoadExistingBtn";
        public const string APP_ICON_SPRITE_OBJ_ID = "ApplicationIconSprite";
        public const string APP_CREATE_BTN_ID = "ApplicationCreateBtn";
        public const string APP_CREATE_RESULT_LABEL_ID = "ApplicationCreateResultLabel";

        public const string CONTAINER_REGISTRY_FOLDOUT_ID = "ContainerRegistryFoldout";
        public const string CONTAINER_REGISTRY_PORT_NUM_ID = "ContainerRegistryPortNumTxt";
        public const string CONTAINER_REGISTRY_TRANSPORT_TYPE_ENUM_ID = "ContainerRegistryProtocolTypeEnumField";
        public const string CONTAINER_NEW_TAG_VERSION_TXT_ID = "ContainerNewVersionTagTxt";
        public const string CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID = "ContainerUseCustomRegistryToggle";
        public const string CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID = "ContainerCustomRegistryWrapper";
        public const string CONTAINER_REGISTRY_URL_TXT_ID = "ContainerRegistryUrlTxt";
        public const string CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID = "ContainerImageRepositoryTxt";
        public const string CONTAINER_USERNAME_TXT_ID = "ContainerUsernameTxt";
        public const string CONTAINER_TOKEN_TXT_ID = "ContainerTokenTxt";
        public const string CONTAINER_BUILD_AND_PUSH_BTN_ID = "ContainerBuildAndPushBtn";
        public const string CONTAINER_BUILD_AND_PUSH_RESULT_LABEL_ID = "ContainerBuildAndPushResultLabel";

        public const string DEPLOYMENTS_FOLDOUT_ID = "DeploymentsFoldout";
        public const string DEPLOYMENTS_REFRESH_BTN_ID = "DeploymentsRefreshBtn";
        public const string DEPLOYMENTS_CREATE_BTN_ID = "DeploymentsCreateBtn";
        public const string DEPLOYMENTS_STATUS_LABEL_ID = "DeploymentsStatusLabel";
        public const string DEPLOYMENTS_CONTAINER_ID = "DeploymentsConnectionGroupBox";
        public const string DEPLOYMENTS_CONNECTION_COPY_URL_BTN_ID = "DeploymentConnectionCopyUrlBtn";
        public const string DEPLOYMENTS_CONNECTION_URL_READONLY_TXT_ID = "DeploymentConnectionUrlReadOnlyTxt"; // Dynamic
        public const string DEPLOYMENTS_CONNECTION_STATUS_LABEL_ID = "DeploymentsConnectionStatusLabel"; // Dynamic
        public const string DEPLOYMENTS_CONNECTION_SERVER_ACTION_STOP_BTN_ID = "DeploymentsConnectionServerStopBtn";
        public const string DEPLOYMENTS_CONNECTION_CONTAINER_LOGS_BTN_ID = "DeploymentsConnectionContainerLogsBtn";

        public const string FOOTER_DOCUMENTATION_BTN_ID = "FooterDocumentationBtn";
        public const string FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID = "FooterNeedMoreGameServersBtn";
        #endregion // UI Element Ids



        //[Obsolete("Hard-coded; not from UI. TODO: Get from UI")] // MIRROR CHANGE: comment this out to avoid import warnings
        public const ApiEnvironment API_ENVIRONMENT = ApiEnvironment.Console;
    }
}
