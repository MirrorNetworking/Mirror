#if UNITY_2021_3_OR_NEWER // MIRROR CHANGE
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Edgegap.Editor.Api;
using Edgegap.Editor.Api.Models;
using Edgegap.Editor.Api.Models.Requests;
using Edgegap.Editor.Api.Models.Results;
using Edgegap.Codice.Utils;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UIElements;
using Application = UnityEngine.Application;
using HttpUtility = Edgegap.Codice.Utils.HttpUtility; // MIRROR CHANGE for Unity 2023 support
#if !EDGEGAP_PLUGIN_SERVERS
using UnityEditor.Build;
#endif

namespace Edgegap.Editor
{
    /// <summary>
    /// Editor logic event handler for "UI Builder" EdgegapWindow.uxml, superceding` EdgegapWindow.cs`.
    /// </summary>
    public class EdgegapWindowV2 : EditorWindow
    {
        #region Vars
        #region Filepaths
        internal string ProjectRootPath => Directory.GetCurrentDirectory();
        internal string ThisScriptPath =>
            Directory.GetFiles(
                ProjectRootPath,
                GetType().Name + ".cs",
                SearchOption.AllDirectories
            )[0];
        #endregion

        #region State Variables
        public static bool IsLogLevelDebug =>
            EdgegapWindowMetadata.LOG_LEVEL == EdgegapWindowMetadata.LogLevel.Debug;
        private bool _isApiTokenVerified; // Toggles the rest of the UI

        private GetRegistryCredentialsResult _credentials;
        private string _userExternalIp;

        private string _containerRegistryUrl;
        private string _containerProject;
        private string _containerUsername;
        private string _containerToken;

        private List<string> _localImages = null;
        private List<string> _storedAppNames = null;
        private List<string> _storedAppVersions = null;

        EdgegapDeploymentsApi _deployAPI;
        #endregion

        #region UI
        private float ProgressCounter = 0;

        #region UI / Containers
        private VisualTreeAsset _visualTree;
        internal string _stylesheetPath =>
            Path.GetDirectoryName(
                AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this))
            );
        private Button _debugBtn;
        private VisualElement _postAuthContainer;
        #endregion

        #region UI / Containers / Connect
        private VisualElement _preAuthContainer;
        private VisualElement _authContainer;
        private Button _joinEdgegapDiscordBtn;
        #endregion

        #region UI / Connect / Pre-Auth
        private Button _edgegapSignInBtn;
        #endregion

        #region UI / Connect / Auth
        private Button _signOutBtn;
        private TextField _apiTokenInput;
        private string _apiToken => _apiTokenInput is null ? "" : _apiTokenInput.value.Trim();
        private Button _apiTokenVerifyBtn;
        private Button _apiTokenGetBtn;
        #endregion

        #region UI / Build
        private Foldout _serverBuildFoldout;
        private Button _infoLinuxRequirementsBtn;
        private Button _installLinuxRequirementsBtn;
        private Label _linuxRequirementsResultLabel;
        private Button _buildParamsBtn;
        private TextField _buildFolderNameInput;
        internal string _buildFolderNameInputDefault => "EdgegapServer";
        private Button _serverBuildBtn;
        private Label _serverBuildResultLabel;
        #endregion

        #region UI / Containerize
        private Foldout _containerizeFoldout;
        private Button _infoDockerRequirementsBtn;
        private Button _validateDockerRequirementsBtn;
        private Label _dockerRequirementsResultLabel;
        private TextField _buildPathInput;
        internal string _buildPathInputDefault => $"Builds/{_buildFolderNameInput.value}";
        private Button _buildPathResetBtn;
        private TextField _containerizeImageNameInput;
        public string _containerizeImageNameInputDefault =>
            Tokenize(Application.productName.ToLowerInvariant());
        private TextField _containerizeImageTagInput;
        internal string _containerizeImageTagInputDefault =>
            EdgegapWindowMetadata.DEFAULT_VERSION_TAG;
        internal string nowUTC => $"{DateTime.UtcNow.ToString("yy.MM.dd-HH.mm.ss")}-UTC";
        private TextField _dockerfilePathInput;
        internal string _dockerfilePathInputDefault =>
            $"{Directory.GetParent(ThisScriptPath).FullName}{Path.DirectorySeparatorChar}Dockerfile";
        private Button _dockerfilePathResetBtn;
        private TextField _optionalDockerParamsInput;
        private Button _containerizeServerBtn;
        private Label _containerizeServerResultLabel;
        #endregion

        #region UI / Test
        private Foldout _localTestFoldout;
        private TextField _localTestImageInput;
        private Button _localTestImageShowDropdownBtn;
        private TextField _localTestDockerRunInput;
        internal string _localTestDockerRunInputDefault => "-p 7777/udp";
        private Button _localTestDeployBtn;
        private Button _localTestTerminateBtn;
        private Button _localTestDiscordHelpBtn;
        private Label _localTestResultLabel;
        private Button _localTestInfoConnectBtn;
        #endregion

        #region UI / Upload App
        private Foldout _createAppFoldout;
        private TextField _createAppNameInput;
        private static readonly Regex _appNameAllowedCharsRegex = new Regex(
            @"^[a-zA-Z0-9_\-+\.]*$"
        );
        private TextField _serverImageNameInput;
        private TextField _serverImageTagInput;
        private Button _portMappingLabelLink;
        private Button _uploadImageCreateAppBtn;
        private Button _appInfoLabelLink;
        private Button _createAppNameShowDropdownBtn;
        #endregion

        #region UI / Deploy
        private Foldout _deployAppFoldout;
        private TextField _deployAppNameInput;
        private TextField _deployAppVersionInput;
        private Button _deployLimitLabelLink;
        private Button _deployAppBtn;
        private Button _stopLastDeployBtn;
        private Button _discordHelpBtn;
        private Label _deployResultLabel;
        private Button _deployAppNameShowDropdownBtn;
        private Button _deployAppVersionShowDropdownBtn;
        #endregion

        #region UI / Next
        private Foldout _nextStepsFoldout;
        private Button _serverConnectLink;
        private Button _gen2MatchmakerLabelLink;
        private Button _lifecycleManageLabelLink;
        #endregion
        #endregion
        #endregion

        #region Unity Integration
        [MenuItem("Tools/Edgegap Hosting")]
        public static void ShowEdgegapToolWindow()
        {
            EdgegapWindowV2 window = GetWindow<EdgegapWindowV2>();
            window.titleContent = new GUIContent("Edgegap Hosting"); // MIRROR CHANGE: 'Edgegap Server Management' is too long for the tab space
            window.maxSize = new Vector2(600, 900);
            window.minSize = window.maxSize;
        }

        // Compiler symbols can be used by other plugin developers to detect presence of Edgegap plugin
        [InitializeOnLoadMethod]
        public static void AddDefineSymbols()
        {
            // check if defined first, otherwise adding the symbol causes an infinite loop of recompilation
#if !EDGEGAP_PLUGIN_SERVERS
            // Get data about current target group
            bool standaloneAndServer = false;
            BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            if (buildTargetGroup == BuildTargetGroup.Standalone)
            {
                StandaloneBuildSubtarget standaloneSubTarget =
                    EditorUserBuildSettings.standaloneBuildSubtarget;
                if (standaloneSubTarget == StandaloneBuildSubtarget.Server)
                    standaloneAndServer = true;
            }

            // Prepare named target, depending on above stuff
            NamedBuildTarget namedBuildTarget;
            if (standaloneAndServer)
                namedBuildTarget = NamedBuildTarget.Server;
            else
                namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);

            // Set universal compiler macro
            PlayerSettings.SetScriptingDefineSymbols(
                namedBuildTarget,
                $"{PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget)};{EdgegapWindowMetadata.KEY_COMPILER_MACRO}"
            );
#endif
        }

        protected void OnEnable()
        {
#if UNITY_2021_3_OR_NEWER // only load stylesheet in supported Unity versions, otherwise it shows errors in U2020
            // Set root VisualElement and style: V2 still uses EdgegapWindow.[uxml|uss]
            // BEGIN MIRROR CHANGE
            _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                $"{_stylesheetPath}{Path.DirectorySeparatorChar}EdgegapWindow.uxml"
            );
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                $"{_stylesheetPath}{Path.DirectorySeparatorChar}EdgegapWindow.uss"
            );
            // END MIRROR CHANGE
            rootVisualElement.styleSheets.Add(styleSheet);
#endif
        }

        public async void CreateGUI()
        {
            // the UI requires 'GroupBox', which is not available in Unity 2019/2020.
            // showing it will break all of Unity's Editor UIs, not just this one.
            // instead, show a warning that the Edgegap plugin only works on Unity 2021+
#if !UNITY_2021_3_OR_NEWER
            Debug.LogWarning(
                "The Edgegap Hosting plugin requires UIToolkit in Unity 2021.3 or newer. Please upgrade your Unity version to use this."
            );
#else
            // Get UI elements from UI Builder
            rootVisualElement.Clear();
            _visualTree.CloneTree(rootVisualElement);

            // Register callbacks and sync UI builder elements to fields here
            InitUIElements();

            // TODO: Load persistent data?

            // Only show the rest of the form if apiToken is verified
            _postAuthContainer.SetEnabled(_isApiTokenVerified);

            await InitializeState(); // API calls
#endif
        }

        /// <summary>The user closed the window. Save the data.</summary>
        protected void OnDisable()
        {
#if UNITY_2021_3_OR_NEWER // only load stylesheet in supported Unity versions, otherwise it shows errors in U2020
            // sometimes this is called without having been registered, throwing NRE
            unregisterUICallbacks();
#endif
        }
        #endregion // Unity Funcs

        #region Init & Cleanup
        /// <summary>
        /// Binds the form inputs to the associated variables and initializes the inputs as required.
        /// Requires the VisualElements to be loaded before this call. Otherwise, the elements cannot be found.
        /// </summary>
        private void InitUIElements()
        {
            setVisualElementsToFields();
            closeDisableGroups();
            registerUICallbacks();
            initToggleDynamicUI();
        }

        /// <summary>Set fields referencing UI Builder's fields. In order of appearance from top-to-bottom.</summary>
        private void setVisualElementsToFields()
        {
            _debugBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEBUG_BTN_ID);
            _postAuthContainer = rootVisualElement.Q<VisualElement>(
                EdgegapWindowMetadata.POST_AUTH_CONTAINER_ID
            );

            _preAuthContainer = rootVisualElement.Q<VisualElement>(
                EdgegapWindowMetadata.SIGN_IN_CONTAINER_ID
            );
            _edgegapSignInBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.SIGN_IN_BTN_ID);

            _authContainer = rootVisualElement.Q<VisualElement>(
                EdgegapWindowMetadata.CONNECTED_CONTAINER_ID
            );
            _signOutBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.SIGN_OUT_BTN_ID);
            _joinEdgegapDiscordBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.JOIN_DISCORD_BTN_ID
            );
            _apiTokenInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.API_TOKEN_TXT_ID);
            _apiTokenVerifyBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.API_TOKEN_VERIFY_BTN_ID
            );
            _apiTokenGetBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.API_TOKEN_GET_BTN_ID
            );

            _serverBuildFoldout = rootVisualElement.Q<Foldout>(
                EdgegapWindowMetadata.SERVER_BUILD_FOLDOUT_ID
            );
            _infoLinuxRequirementsBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.LINUX_REQUIREMENTS_LINK_ID
            );
            _installLinuxRequirementsBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.INSTALL_LINUX_BTN_ID
            );
            _linuxRequirementsResultLabel = rootVisualElement.Q<Label>(
                EdgegapWindowMetadata.INSTALL_LINUX_RESULT_LABEL_ID
            );
            _buildParamsBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.SERVER_BUILD_PARAM_BTN_ID
            );
            _buildFolderNameInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.SERVER_BUILD_FOLDER_TXT_ID
            );
            _serverBuildBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.SERVER_BUILD_BTN_ID
            );
            _serverBuildResultLabel = rootVisualElement.Q<Label>(
                EdgegapWindowMetadata.SERVER_BUILD_RESULT_LABEL_ID
            );

            _containerizeFoldout = rootVisualElement.Q<Foldout>(
                EdgegapWindowMetadata.CONTAINERIZE_SERVER_FOLDOUT_ID
            );
            _infoDockerRequirementsBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.DOCKER_INSTALL_LINK_ID
            );
            _validateDockerRequirementsBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.VALIDATE_DOCKER_INSTALL_BTN_ID
            );
            _dockerRequirementsResultLabel = rootVisualElement.Q<Label>(
                EdgegapWindowMetadata.VALIDATE_DOCKER_RESULT_LABEL_ID
            );
            _buildPathInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.CONTAINERIZE_SERVER_BUILD_PATH_TXT_ID
            );
            _buildPathResetBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.CONTAINERIZE_BUILD_PATH_RESET_BTN_ID
            );
            _containerizeImageNameInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.CONTAINERIZE_IMAGE_NAME_TXT_ID
            );
            _containerizeImageTagInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.CONTAINERIZE_IMAGE_TAG_TXT_ID
            );
            _dockerfilePathInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.DOCKERFILE_PATH_TXT_ID
            );
            _dockerfilePathResetBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.DOCKERFILE_PATH_RESET_BTN_ID
            );
            _optionalDockerParamsInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.DOCKER_BUILD_PARAMS_TXT_ID
            );
            _containerizeServerBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.CONTAINERIZE_SERVER_BTN_ID
            );
            _containerizeServerResultLabel = rootVisualElement.Q<Label>(
                EdgegapWindowMetadata.CONTAINERIZE_SERVER_RESULT_LABEL_TXT
            );

            _localTestFoldout = rootVisualElement.Q<Foldout>(
                EdgegapWindowMetadata.LOCAL_TEST_FOLDOUT_ID
            );
            _localTestImageInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.LOCAL_TEST_IMAGE_TXT_ID
            );
            ;
            _localTestImageShowDropdownBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.LOCAL_TEST_IMAGE_SHOW_DROPDOWN_BTN_ID
            );
            ;
            _localTestDockerRunInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.LOCAL_TEST_DOCKER_RUN_TXT_ID
            );
            _localTestDeployBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.LOCAL_TEST_DEPLOY_BTN_ID
            );
            ;
            _localTestTerminateBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.LOCAL_TEST_TERMINATE_BTN_ID
            );
            ;
            _localTestDiscordHelpBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.LOCAL_TEST_DISCORD_HELP_BTN_ID
            );
            ;
            _localTestResultLabel = rootVisualElement.Q<Label>(
                EdgegapWindowMetadata.LOCAL_TEST_RESULT_LABEL_ID
            );
            _localTestInfoConnectBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.LOCAL_TEST_CONNECT_LABEL_LINK_ID
            );

            _createAppFoldout = rootVisualElement.Q<Foldout>(
                EdgegapWindowMetadata.CREATE_APP_FOLDOUT_ID
            );
            _createAppNameInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.CREATE_APP_NAME_TXT_ID
            );
            _createAppNameShowDropdownBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.CREATE_APP_NAME_SHOW_DROPDOWN_BTN_ID
            );
            _serverImageNameInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.CREATE_APP_IMAGE_NAME_TXT_ID
            );
            _serverImageTagInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.CREATE_APP_IMAGE_TAG_TXT_ID
            );
            _portMappingLabelLink = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.PORT_MAPPING_LABEL_LINK_ID
            );
            _uploadImageCreateAppBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.PUSH_IMAGE_CREATE_APP_BTN_ID
            );
            _appInfoLabelLink = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.EDGEGAP_APP_LABEL_LINK_ID
            );

            _deployAppFoldout = rootVisualElement.Q<Foldout>(
                EdgegapWindowMetadata.DEPLOY_APP_FOLDOUT_ID
            );
            _deployAppNameInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.DEPLOY_APP_NAME_TXT_ID
            );
            _deployAppVersionInput = rootVisualElement.Q<TextField>(
                EdgegapWindowMetadata.DEPLOY_APP_TAG_VERSION_TXT_ID
            );
            _deployLimitLabelLink = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.DEPLOY_LIMIT_LABEL_LINK_ID
            );
            _deployAppBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOY_START_BTN_ID);
            _stopLastDeployBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.DEPLOY_STOP_BTN_ID
            );
            _discordHelpBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.DEPLOY_DISCORD_HELP_BTN_ID
            );
            _deployResultLabel = rootVisualElement.Q<Label>(
                EdgegapWindowMetadata.DEPLOY_RESULT_LABEL_TXT
            );
            _deployAppNameShowDropdownBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.DEPLOY_APP_NAME_SHOW_DROPDOWN_BTN_ID
            );
            _deployAppVersionShowDropdownBtn = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.DEPLOY_APP_VERSION_SHOW_DROPDOWN_BTN_ID
            );

            _nextStepsFoldout = rootVisualElement.Q<Foldout>(
                EdgegapWindowMetadata.NEXT_STEPS_FOLDOUT_ID
            );
            _serverConnectLink = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.NEXT_STEPS_SERVER_CONNECT_LINK_ID
            );
            _gen2MatchmakerLabelLink = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.NEXT_STEPS_MANAGED_MATCHMAKER_LABEL_LINK_ID
            );
            _lifecycleManageLabelLink = rootVisualElement.Q<Button>(
                EdgegapWindowMetadata.NEXT_STEPS_LIFECYCLE_LABEL_LINK_ID
            );
        }

        private void closeDisableGroups()
        {
            _serverBuildFoldout.SetValueWithoutNotify(false);
            _containerizeFoldout.SetValueWithoutNotify(false);
            _localTestFoldout.SetValueWithoutNotify(false);
            _createAppFoldout.SetValueWithoutNotify(false);
            _deployAppFoldout.SetValueWithoutNotify(false);
            _nextStepsFoldout.SetValueWithoutNotify(false);

            _serverBuildFoldout.SetEnabled(false);
            _containerizeFoldout.SetEnabled(false);
            _localTestFoldout.SetEnabled(false);
            _createAppFoldout.SetEnabled(false);
            _deployAppFoldout.SetEnabled(false);
            _nextStepsFoldout.SetEnabled(false);
        }

        /// <summary>
        /// Register UI callbacks. We'll want to save for persistence, validate, etc
        /// </summary>
        private void registerUICallbacks()
        {
            _debugBtn.clickable.clicked += onDebugBtnClick;

            _edgegapSignInBtn.clickable.clicked += OnEdgegapSignInBtnClick;
            _apiTokenGetBtn.clickable.clicked += OpenGetTokenUrl;
            _apiTokenInput.RegisterCallback<FocusInEvent>(onApiTokenInputFocusIn);
            _apiTokenInput.RegisterCallback<FocusOutEvent>(onApiTokenInputFocusOut);
            _apiTokenVerifyBtn.clickable.clicked += onApiTokenVerifyBtnClick;
            _signOutBtn.clickable.clicked += OnSignOutBtnClickAsync;
            _joinEdgegapDiscordBtn.clickable.clicked += OnDiscordBtnClick;

            _infoLinuxRequirementsBtn.clickable.clicked += OnLinuxInfoClick;
            _installLinuxRequirementsBtn.clickable.clicked += OnInstallLinuxBtnClick;
            _buildParamsBtn.clickable.clicked += OnOpenBuildParamsBtnClick;
            _buildFolderNameInput.RegisterCallback<FocusOutEvent>(OnFolderNameInputFocusOut);
            _serverBuildBtn.clickable.clicked += OnBuildServerBtnClick;

            _infoDockerRequirementsBtn.clickable.clicked += OnDockerInfoClick;
            _validateDockerRequirementsBtn.clickable.clicked += OnValidateDockerBtnClick;
            _buildPathInput.RegisterCallback<FocusInEvent>(OnBuildPathInputFocusIn);
            _buildPathResetBtn.clickable.clicked += OnResetBuildPathBtnClick;
            _containerizeImageNameInput.RegisterValueChangedCallback(OnContainerizeInputsChanged);
            _containerizeImageNameInput.RegisterCallback<FocusInEvent>(
                OnContainerizeImageNameInputFocusIn
            );
            _containerizeImageNameInput.RegisterCallback<FocusOutEvent>(
                OnContainerizeImageNameInputFocusOut
            );
            _containerizeImageTagInput.RegisterValueChangedCallback(OnContainerizeInputsChanged);
            _containerizeImageTagInput.RegisterCallback<FocusInEvent>(
                OnContainerizeImageTagInputFocusIn
            );
            _containerizeImageTagInput.RegisterCallback<FocusOutEvent>(
                OnContainerizeImageTagInputFocusOut
            );
            _dockerfilePathResetBtn.clickable.clicked += OnResetDockerfilePathBtnClick;
            _dockerfilePathInput.RegisterCallback<FocusInEvent>(OnDockerfilePathInputFocusIn);
            _containerizeServerBtn.clickable.clicked += OnContainerizeBtnClickAsync;

            _localTestImageInput.RegisterValueChangedCallback(OnLocalTestInputsChanged);
            _localTestImageShowDropdownBtn.clickable.clicked += OnLocalTestImageDropdownClick;
            _localTestImageInput.RegisterCallback<FocusInEvent>(OnLocalTestInputFocusIn);
            _localTestDockerRunInput.RegisterCallback<FocusOutEvent>(
                OnLocalTestDockerParamsFocusOut
            );
            _localTestDeployBtn.clickable.clicked += OnLocalTestDeployClick;
            _localTestTerminateBtn.clickable.clicked += OnLocalTestTerminateCLick;
            _localTestDiscordHelpBtn.clickable.clicked += OnDiscordBtnClick;
            _localTestInfoConnectBtn.clickable.clicked += OnLocalContainerConnectLinkClick;

            _createAppNameShowDropdownBtn.clickable.clicked += OnCreateAppNameDropdownClick;
            _createAppNameInput.RegisterCallback<FocusOutEvent>(OnCreateAppNameInputFocusOut);
            _serverImageNameInput.RegisterValueChangedCallback(OnCreateInputsChanged);
            _serverImageTagInput.RegisterValueChangedCallback(OnCreateInputsChanged);
            _portMappingLabelLink.clickable.clicked += OnPortsMappingLinkClick;
            _uploadImageCreateAppBtn.clickable.clicked += OnUploadImageCreateAppBtnClickAsync;
            _appInfoLabelLink.clickable.clicked += OnYourAppLinkClick;

            _deployAppNameInput.RegisterCallback<FocusInEvent>(OnDeployAppNameInputFocusIn);
            _deployAppNameInput.RegisterValueChangedCallback(OnDeployAppNameInputChanged);
            _deployAppNameShowDropdownBtn.clickable.clicked += OnDeployAppNameDropdownClick;
            _deployAppVersionInput.RegisterCallback<FocusInEvent>(OnDeployAppVersionInputFocusIn);
            _deployAppVersionInput.RegisterValueChangedCallback(OnDeployAppVersionInputChanged);
            _deployAppVersionShowDropdownBtn.clickable.clicked += OnDeployAppVersionDropdownClick;
            _deployLimitLabelLink.clickable.clicked += OnDeployLimitLinkClick;
            _deployAppBtn.clickable.clicked += OnDeploymentCreateBtnClick;
            _stopLastDeployBtn.clickable.clicked += OnStopLastDeployClick;
            _discordHelpBtn.clickable.clicked += OnDiscordBtnClick;

            _serverConnectLink.clickable.clicked += OnServerConnectLinkClick;
            _gen2MatchmakerLabelLink.clickable.clicked += OnGen2MatchmakerLinkClick;
            _lifecycleManageLabelLink.clickable.clicked += OnScalingLifecycleLinkClick;
        }

        /// <summary>
        /// Prevents memory leaks, mysterious errors and "ghost" values set from a previous session.
        /// Should parity the opposite of registerUICallbacks().
        /// </summary>
        private void unregisterUICallbacks()
        {
            _debugBtn.clickable.clicked -= onDebugBtnClick;

            _edgegapSignInBtn.clickable.clicked -= OnEdgegapSignInBtnClick;
            _apiTokenGetBtn.clickable.clicked -= OpenGetTokenUrl;
            _apiTokenInput.UnregisterCallback<FocusInEvent>(onApiTokenInputFocusIn);
            _apiTokenInput.UnregisterCallback<FocusOutEvent>(onApiTokenInputFocusOut);
            _apiTokenVerifyBtn.clickable.clicked -= onApiTokenVerifyBtnClick;
            _signOutBtn.clickable.clicked -= OnSignOutBtnClickAsync;
            _joinEdgegapDiscordBtn.clickable.clicked -= OnDiscordBtnClick;

            _infoLinuxRequirementsBtn.clickable.clicked -= OnLinuxInfoClick;
            _installLinuxRequirementsBtn.clickable.clicked -= OnInstallLinuxBtnClick;
            _buildParamsBtn.clickable.clicked -= OnOpenBuildParamsBtnClick;
            _buildFolderNameInput.UnregisterCallback<FocusOutEvent>(OnFolderNameInputFocusOut);
            _serverBuildBtn.clickable.clicked -= OnBuildServerBtnClick;

            _infoDockerRequirementsBtn.clickable.clicked -= OnDockerInfoClick;
            _validateDockerRequirementsBtn.clickable.clicked -= OnValidateDockerBtnClick;
            _buildPathInput.UnregisterCallback<FocusInEvent>(OnBuildPathInputFocusIn);
            _buildPathResetBtn.clickable.clicked -= OnResetBuildPathBtnClick;
            _containerizeImageNameInput.UnregisterValueChangedCallback(OnContainerizeInputsChanged);
            _containerizeImageNameInput.UnregisterCallback<FocusInEvent>(
                OnContainerizeImageNameInputFocusIn
            );
            _containerizeImageNameInput.UnregisterCallback<FocusOutEvent>(
                OnContainerizeImageNameInputFocusOut
            );
            _containerizeImageTagInput.UnregisterValueChangedCallback(OnContainerizeInputsChanged);
            _containerizeImageTagInput.UnregisterCallback<FocusInEvent>(
                OnContainerizeImageTagInputFocusIn
            );
            _containerizeImageTagInput.UnregisterCallback<FocusOutEvent>(
                OnContainerizeImageTagInputFocusOut
            );
            _dockerfilePathResetBtn.clickable.clicked -= OnResetDockerfilePathBtnClick;
            _dockerfilePathInput.UnregisterCallback<FocusInEvent>(OnDockerfilePathInputFocusIn);
            _containerizeServerBtn.clickable.clicked -= OnContainerizeBtnClickAsync;

            _localTestImageInput.UnregisterCallback<FocusInEvent>(OnLocalTestInputFocusIn);
            _localTestImageInput.UnregisterValueChangedCallback(OnLocalTestInputsChanged);
            _localTestImageShowDropdownBtn.clickable.clicked -= OnLocalTestImageDropdownClick;
            _localTestDockerRunInput.UnregisterCallback<FocusOutEvent>(
                OnLocalTestDockerParamsFocusOut
            );
            _localTestDeployBtn.clickable.clicked -= OnLocalTestDeployClick;
            _localTestTerminateBtn.clickable.clicked -= OnLocalTestTerminateCLick;
            _localTestDiscordHelpBtn.clickable.clicked -= OnDiscordBtnClick;
            _localTestInfoConnectBtn.clickable.clicked -= OnLocalContainerConnectLinkClick;

            _createAppNameShowDropdownBtn.clickable.clicked -= OnCreateAppNameDropdownClick;
            _createAppNameInput.UnregisterCallback<FocusOutEvent>(OnCreateAppNameInputFocusOut);
            _serverImageNameInput.UnregisterValueChangedCallback(OnCreateInputsChanged);
            _serverImageTagInput.UnregisterValueChangedCallback(OnCreateInputsChanged);
            _portMappingLabelLink.clickable.clicked -= OnPortsMappingLinkClick;
            _uploadImageCreateAppBtn.clickable.clicked -= OnUploadImageCreateAppBtnClickAsync;
            _appInfoLabelLink.clickable.clicked -= OnYourAppLinkClick;

            _deployAppNameInput.UnregisterCallback<FocusInEvent>(OnDeployAppNameInputFocusIn);
            _deployAppNameInput.UnregisterValueChangedCallback(OnDeployAppNameInputChanged);
            _deployAppNameShowDropdownBtn.clickable.clicked -= OnDeployAppNameDropdownClick;
            _deployAppVersionInput.UnregisterCallback<FocusInEvent>(OnDeployAppVersionInputFocusIn);
            _deployAppVersionInput.UnregisterValueChangedCallback(OnDeployAppVersionInputChanged);
            _deployAppVersionShowDropdownBtn.clickable.clicked -= OnDeployAppVersionDropdownClick;
            _deployLimitLabelLink.clickable.clicked -= OnDeployLimitLinkClick;
            _deployAppBtn.clickable.clicked -= OnDeploymentCreateBtnClick;
            _stopLastDeployBtn.clickable.clicked -= OnStopLastDeployClick;
            _discordHelpBtn.clickable.clicked -= OnDiscordBtnClick;

            _serverConnectLink.clickable.clicked -= OnServerConnectLinkClick;
            _gen2MatchmakerLabelLink.clickable.clicked -= OnGen2MatchmakerLinkClick;
            _lifecycleManageLabelLink.clickable.clicked -= OnScalingLifecycleLinkClick;
        }

        private void initToggleDynamicUI()
        {
            hideResultLabels();

            // ApiToken
            if (string.IsNullOrEmpty(_apiToken))
            {
                string apiTokenBase64Str = EditorPrefs.GetString(
                    EdgegapWindowMetadata.API_TOKEN_KEY_STR,
                    null
                );

                if (apiTokenBase64Str == null)
                    return;

                string decodedApiToken = Base64Decode(apiTokenBase64Str);
                _apiTokenInput.SetValueWithoutNotify(decodedApiToken);
            }

            _debugBtn.visible = EdgegapWindowMetadata.SHOW_DEBUG_BTN;
        }

        private void ResetState()
        {
            // reset input values
            _buildFolderNameInput.SetValueWithoutNotify("");
            _buildPathInput.SetValueWithoutNotify("");
            _containerizeImageNameInput.SetValueWithoutNotify("");
            _containerizeImageTagInput.SetValueWithoutNotify("");
            _dockerfilePathInput.SetValueWithoutNotify("");
            _optionalDockerParamsInput.SetValueWithoutNotify("");
            _createAppNameInput.SetValueWithoutNotify("");
            _serverImageNameInput.SetValueWithoutNotify("");
            _serverImageTagInput.SetValueWithoutNotify("");
            _deployAppNameInput.SetValueWithoutNotify("");
            _deployAppVersionInput.SetValueWithoutNotify("");

            _isApiTokenVerified = false;
            _credentials = null;
            _userExternalIp = null;
            _containerRegistryUrl = null;
            _containerProject = null;
            _containerUsername = null;
            _containerToken = null;
            _localImages = null;
            _storedAppNames = null;
            _storedAppVersions = null;

            hideResultLabels();
            closeDisableGroups();
            ToggleIsConnectedContainers(false);
        }

        /// <summary>For example, result labels (success/err) should be hidden on init</summary>
        private void hideResultLabels()
        {
            _serverBuildResultLabel.visible = false;
            _containerizeServerResultLabel.visible = false;
            _localTestResultLabel.style.display = DisplayStyle.None;
            _deployResultLabel.style.display = DisplayStyle.None;
            _linuxRequirementsResultLabel.visible = false;
            _dockerRequirementsResultLabel.visible = false;
        }
        #endregion

        #region Fns / Debug
        /// <summary>
        /// Experiment here! You may want to log what you're doing
        /// in case you inadvertently leave it on.
        /// </summary>
        private void onDebugBtnClick() => debugEnableAllGroups();

        private void debugEnableAllGroups()
        {
            Debug.Log("debugEnableAllGroups");

            _serverBuildFoldout.SetEnabled(true);
            _containerizeFoldout.SetEnabled(true);
            _localTestFoldout.SetEnabled(true);
            _createAppFoldout.SetEnabled(true);
            _deployAppFoldout.SetEnabled(true);
            _nextStepsFoldout.SetEnabled(true);
        }
        #endregion

        #region Fns / Connect
        /// <summary>
        /// "Sign in" btn click
        /// </summary>
        private void OnEdgegapSignInBtnClick()
        {
            if (!_isApiTokenVerified)
            {
                OpenGetTokenUrl();
            }
            ToggleIsConnectedContainers(true);
        }

        private void OpenGetTokenUrl()
        {
            OpenEdgegapURL(EdgegapWindowMetadata.EDGEGAP_GET_A_TOKEN_URL);
        }

        private void onApiTokenInputFocusIn(FocusInEvent evt)
        {
            _apiTokenInput.isPasswordField = false;
        }

        private void onApiTokenInputFocusOut(FocusOutEvent evt)
        {
            _apiTokenInput.isPasswordField = true;

            _isApiTokenVerified = false;
            _postAuthContainer.SetEnabled(false);
            closeDisableGroups();

            // Toggle "Verify" btn on 1+ char entered
            if (_apiToken.Length > 0)
            {
                onApiTokenVerifyBtnClick();
            }
        }

        private void onApiTokenVerifyBtnClick()
        {
            ResetState();
            initToggleDynamicUI();
            _ = verifyApiTokenGetRegistryCreds();
            _ = InitializeState();
            _ = checkForUpdates();
        }

        /// <summary>
        /// Verifies token => apps/container groups -> gets registry creds (if any).
        /// </summary>
        private async Task verifyApiTokenGetRegistryCreds()
        {
            if (IsLogLevelDebug)
                Debug.Log("verifyApiTokenGetRegistryCredsAsync");

            // Disable most ui while we verify
            _isApiTokenVerified = false;
            _signOutBtn.SetEnabled(false);
            UpdateUI();
            hideResultLabels();

            EdgegapWizardApi wizardApi = new EdgegapWizardApi(
                EdgegapWindowMetadata.API_ENVIRONMENT,
                _apiToken,
                EdgegapWindowMetadata.LOG_LEVEL
            );
            EdgegapHttpResult initQuickStartResultCode = await wizardApi.InitQuickStart();

            _signOutBtn.SetEnabled(true);
            _isApiTokenVerified = initQuickStartResultCode.IsResultCode204;

            if (!_isApiTokenVerified)
            {
                UpdateUI();
                return;
            }

            // Verified: Let's see if we have active registry credentials // TODO: This will later be a result model
            EdgegapHttpResult<GetRegistryCredentialsResult> getRegistryCredentialsResult =
                await wizardApi.GetRegistryCredentials();

            if (getRegistryCredentialsResult.IsResultCode200)
            {
                // Success
                _credentials = getRegistryCredentialsResult.Data;
                EditorPrefs.SetString(
                    EdgegapWindowMetadata.API_TOKEN_KEY_STR,
                    Base64Encode(_apiToken)
                );

                if (IsLogLevelDebug)
                    Debug.Log("SetContainerRegistryData");

                if (_credentials == null)
                    throw new Exception($"!{nameof(_credentials)}");

                _containerRegistryUrl = _credentials.RegistryUrl;
                _containerProject = _credentials.Project;
                _containerUsername = _credentials.Username;
                _containerToken = _credentials.Token;
                Debug.Log("Edgegap API token verified successfully.");
            }
            else
            {
                ShowErrorDialog(
                    $"Couldn't retrieve Edgegap registry credentials, try re-logging.\n\n{getRegistryCredentialsResult.Data.ToString()}"
                );
            }

            // Unlock the rest of the form, whether we prefill the container registry or not
            UpdateUI();
        }

        /// <summary>
        /// Fetch latest github release and compare with local package.json version
        /// </summary>
        private async Task checkForUpdates()
        {
            // get local package.json version
            DirectoryInfo thisScriptDir = new DirectoryInfo(ThisScriptPath);
            PackageJSON local = PackageJSON.PackageJSONFromJSON(
                $"{thisScriptDir.Parent.Parent.ToString()}{Path.DirectorySeparatorChar}package.json"
            );

            // get latest release from github repository
            string releaseJSON = await GithubRelease.GithubReleaseFromAPI();
            GithubRelease latest = GithubRelease.GithubReleaseFromJSON(releaseJSON);

            if (local.version != latest.name)
            {
                Debug.LogWarning(
                    $"Please update your Edgegap Quickstart plugin - local version `{local.version}` < latest version `{latest.name}`. See https://github.com/edgegap/edgegap-unity-plugin."
                );
            }
        }

        /// <summary>
        /// "Sign out" btn click
        /// </summary>
        private void OnSignOutBtnClickAsync()
        {
            EditorPrefs.DeleteKey(EdgegapWindowMetadata.API_TOKEN_KEY_STR);
            _apiTokenInput.SetValueWithoutNotify("");
            ResetState();
        }

        /// <summary>
        /// Change between the view when connected or the view when not connected
        /// </summary>
        /// <param name="isConnected"></param>
        private void ToggleIsConnectedContainers(bool isConnected)
        {
            _preAuthContainer.SetEnabled(!isConnected);
            _preAuthContainer.style.display = isConnected ? DisplayStyle.None : DisplayStyle.Flex;

            _authContainer.SetEnabled(isConnected);
            _authContainer.style.display = isConnected ? DisplayStyle.Flex : DisplayStyle.None;
        }
        #endregion

        #region Fns / Build
        private void OnLinuxInfoClick() =>
            OpenEdgegapDocsURL(EdgegapWindowMetadata.EDGEGAP_DOC_PLUGIN_GUIDE_PATH);

        /// <summary>
        /// Linux server build requirements install btn click
        /// </summary>
        private async void OnInstallLinuxBtnClick()
        {
            if (
                !BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.Standalone,
                    BuildTarget.StandaloneLinux64
                )
            )
            {
                //ProgressCounter = 0;

                //await EdgegapBuildUtils.InstallLinuxModules(Application.unityVersion,
                //    outputReciever: status => ShowWorkInProgress("Installing linux support modules", status),
                //    errorReciever: (msg) => OnBuildContainerizeUploadError(msg, _linuxRequirementsResultLabel, "There was a problem.")
                //);

                //OnBuildContainerizeUploadSuccess(_linuxRequirementsResultLabel, "Requirements installed. Don't forget to restart Unity.");

                ShowErrorDialog(
                    null,
                    _linuxRequirementsResultLabel,
                    "Requirements not currently installed."
                );
                await Task.Delay(1);
                OpenEdgegapDocsURL(EdgegapWindowMetadata.EDGEGAP_DOC_PLUGIN_GUIDE_PATH);
            }
            else
            {
                OnBuildContainerizeUploadSuccess(
                    _linuxRequirementsResultLabel,
                    "Requirements installed."
                );
            }
        }

        /// <summary>
        /// Open Unity build settings btn click
        /// </summary>
        private void OnOpenBuildParamsBtnClick()
        {
#if UNITY_2021_3_OR_NEWER
            EditorWindow.GetWindow(
                System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor")
            );
#else
            EditorApplication.ExecuteMenuItem("File/Build Settings...");
#endif
        }

        private void OnFolderNameInputFocusOut(FocusOutEvent evt)
        {
            if (string.IsNullOrEmpty(_buildFolderNameInput.value))
            {
                _buildFolderNameInput.value = _buildFolderNameInputDefault;
            }
        }

        /// <summary>
        /// "Build server" btn click
        /// Process UI + validation before/after API logic
        /// </summary>
        private void OnBuildServerBtnClick()
        {
            try
            {
                _serverBuildBtn.SetEnabled(false);
                hideResultLabels();

                // build server
                if (IsLogLevelDebug)
                    Debug.Log("buildServer");
                ProgressCounter = 0;

                if (
                    !BuildPipeline.IsBuildTargetSupported(
                        BuildTargetGroup.Standalone,
                        BuildTarget.StandaloneLinux64
                    )
                )
                {
                    throw new Exception(
                        $"Linux Build Support is missing.\n\nPlease install it via the plugin, or open Unity Hub -> Installs -> Unity {Application.unityVersion} -> Add Modules -> Linux Build Support (IL2CPP & Mono & Dedicated Server) -> Install\n\nAfterwards restart Unity!"
                    );
                }

                string folderName = !string.IsNullOrEmpty(_buildFolderNameInput.value)
                    ? _buildFolderNameInput.value
                    : _buildFolderNameInputDefault;

                BuildReport buildResult = EdgegapBuildUtils.BuildServer(folderName);
                if (buildResult.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogWarning(buildResult.summary.result.ToString());
                }
                else
                {
                    OnBuildContainerizeUploadSuccess(_serverBuildResultLabel, "Build succeeded.");
                }

                _containerizeFoldout.SetValueWithoutNotify(true);
                _buildPathInput.SetValueWithoutNotify(_buildPathInputDefault);
            }
            catch (Exception e)
            {
                Debug.LogError($"OnBuildServerBtnClick Error: {e}");
                ShowErrorDialog(e.Message, _serverBuildResultLabel, "Build failed (see logs).");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _serverBuildBtn.SetEnabled(true);
            }
        }
        #endregion

        #region Fns / Containerize
        private void OnDockerInfoClick() =>
            OpenEdgegapDocsURL(EdgegapWindowMetadata.EDGEGAP_DOC_USAGE_REQUIREMENTS_PATH);

        /// <summary>
        /// Validate Docker installation btn click
        /// </summary>
        private void OnValidateDockerBtnClick()
        {
            _ = ValidateDockerRequirement();
        }

        private async Task<string> ValidateDockerRequirement()
        {
            _validateDockerRequirementsBtn.SetEnabled(false);
            hideResultLabels();
            string error;
            try
            {
                error = await EdgegapBuildUtils.DockerSetupAndInstallationCheck(
                    _dockerfilePathInputDefault
                );
            }
            catch (Exception e)
            {
                error = e.Message;
            }
            _validateDockerRequirementsBtn.SetEnabled(true);

            if (!string.IsNullOrEmpty(error))
            {
                ShowErrorDialog(
                    error.Contains("docker daemon is not running")
                    || error.Contains("dockerDesktop")
                        ? string.Join(
                            "\n\n",
                            new string[]
                            {
                                "Docker is installed, but the daemon/app (e.g. Docker Desktop) is not running.",
                                "Please start Docker and try again.",
                            }
                        )
                        : string.Join(
                            "\n\n",
                            new string[]
                            {
                                "Docker installation not found. Docker can be downloaded from:",
                                "https://www.docker.com/",
                            }
                        ),
                    _dockerRequirementsResultLabel,
                    "There was a problem."
                );
                return error;
            }
            else
            {
                OnBuildContainerizeUploadSuccess(
                    _dockerRequirementsResultLabel,
                    "Docker is running."
                );
            }
            return null;
        }

        /// <summary>
        /// When field gains focus, open File Explorer to select folder path
        /// </summary>
        /// <param name="evt"></param>
        private void OnBuildPathInputFocusIn(FocusInEvent evt)
        {
            string selectedPath = EditorUtility.OpenFolderPanel(
                "Select Build Folder",
                ProjectRootPath,
                ""
            );

            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.Contains(ProjectRootPath.Replace('\\', '/')))
                {
                    string pathFromProjectRoot = selectedPath.Split(
                        ProjectRootPath.Replace('\\', '/') + '/'
                    )[1];
                    _buildPathInput.value = pathFromProjectRoot;
                }
                else
                {
                    ShowErrorDialog(
                        "The selected build folder couldn't be found within the project."
                    );
                }
            }
        }

        /// <summary>
        /// Reset Build Path input value btn click
        /// </summary>
        private void OnResetBuildPathBtnClick()
        {
            _buildPathInput.value = _buildPathInputDefault;
        }

        /// <summary>
        /// On change, toggle containerize btn if all required inputs in Containerize section are filled
        /// </summary>
        /// <param name="evt"></param>
        private void OnContainerizeInputsChanged(ChangeEvent<string> evt)
        {
            _containerizeServerBtn.SetEnabled(CheckFilledContainerizeServerInputs());
        }

        private bool CheckFilledContainerizeServerInputs()
        {
            return _containerizeImageNameInput.value.Length > 0
                && _containerizeImageTagInput.value.Length > 0;
        }

        private void OnContainerizeImageNameInputFocusIn(FocusInEvent evt)
        {
            TogglePlaceholder(
                _containerizeImageNameInput,
                _containerizeImageNameInputDefault,
                true
            );
        }

        private void OnContainerizeImageNameInputFocusOut(FocusOutEvent evt)
        {
            TogglePlaceholder(
                _containerizeImageNameInput,
                _containerizeImageNameInputDefault,
                false
            );
        }

        private void OnContainerizeImageTagInputFocusIn(FocusInEvent evt)
        {
            TogglePlaceholder(_containerizeImageTagInput, _containerizeImageTagInputDefault, true);
        }

        private void OnContainerizeImageTagInputFocusOut(FocusOutEvent evt)
        {
            TogglePlaceholder(_containerizeImageTagInput, _containerizeImageTagInputDefault, false);
        }

        /// <summary>
        /// When field gains focus, open File Explorer to select file path
        /// </summary>
        /// <param name="evt"></param>
        private void OnDockerfilePathInputFocusIn(FocusInEvent evt)
        {
            string selectedPath = EditorUtility.OpenFilePanel(
                "Select Dockerfile",
                ProjectRootPath,
                ""
            );

            if (!string.IsNullOrEmpty(selectedPath))
            {
                _dockerfilePathInput.value = selectedPath;
            }
        }

        /// <summary>
        /// Reset Dockerfile Path input value btn click
        /// </summary>
        private void OnResetDockerfilePathBtnClick()
        {
            _dockerfilePathInput.value = _dockerfilePathInputDefault;
        }

        /// <summary>
        /// "Containerize with Docker" btn click
        /// Process UI + validation before/after API logic
        /// </summary>
        private async void OnContainerizeBtnClickAsync()
        {
            if (!string.IsNullOrEmpty(await ValidateDockerRequirement()))
            {
                return;
            }
            try
            {
                _containerizeServerBtn.SetEnabled(false);
                hideResultLabels();

                // build docker image
                if (IsLogLevelDebug)
                    Debug.Log("buildDockerImageAsync");
                ProgressCounter = 0;

                string dockerfilePath =
                    _dockerfilePathInput.value.Length > 0
                        ? _dockerfilePathInput.value
                        : _dockerfilePathInputDefault;

                string extraParams =
                    _optionalDockerParamsInput.value.Length > 0
                        ? _optionalDockerParamsInput.value
                        : null;

                if (_buildPathInput.value.Length > 0)
                {
                    if (string.IsNullOrEmpty(extraParams))
                    {
                        extraParams = $"--build-arg SERVER_BUILD_PATH=\"{_buildPathInput.value}\"";
                    }
                    else
                    {
                        extraParams +=
                            $" --build-arg SERVER_BUILD_PATH=\"{_buildPathInput.value}\"";
                    }
                }

                string imageName = Tokenize(_containerizeImageNameInput.value);
                string imageRepo = $"{_containerProject}/{imageName}";
                string tag =
                    _containerizeImageTagInput.value == _containerizeImageTagInputDefault
                        ? nowUTC
                        : _containerizeImageTagInput.value;

                await EdgegapBuildUtils.RunCommand_DockerBuild(
                    dockerfilePath,
                    _containerRegistryUrl,
                    imageRepo,
                    tag,
                    ProjectRootPath,
                    status => ShowWorkInProgress("Building Docker Image", status),
                    extraParams ?? null
                );

                OnBuildContainerizeUploadSuccess(
                    _containerizeServerResultLabel,
                    "Containerization succeeded."
                );

                string lowercaseImageName = _containerizeImageNameInput.value;

                _localTestFoldout.SetValueWithoutNotify(true);
                _localTestImageInput.value = $"{lowercaseImageName}:{tag}";
                _localTestImageShowDropdownBtn.SetEnabled(false);
                _serverImageNameInput.value = lowercaseImageName;
                _serverImageTagInput.value = tag;

                await GetLocalDockerImagesAsync();

                if (_localImages is not null && _localImages.Count > 0)
                {
                    _localTestImageShowDropdownBtn.SetEnabled(true);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Containerization Error: {e}");
                ShowErrorDialog(
                    e.Message,
                    _containerizeServerResultLabel,
                    "Containerization failed (see logs)."
                );
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _containerizeServerBtn.SetEnabled(true);
            }
        }

        private async Task GetLocalDockerImagesAsync()
        {
            if (IsLogLevelDebug)
                Debug.Log("GetLocalDockerImages");

            _localImages = new List<string>();

            await EdgegapBuildUtils.RunCommand_DockerImage(
                img =>
                {
                    if (img.Contains($"{_containerRegistryUrl}/{_containerProject}/"))
                    {
                        string shortImg = img.Split(
                            $"{_containerRegistryUrl}/{_containerProject}/"
                        )[1];
                        _localImages.Add(shortImg);
                    }
                },
                error =>
                    ShowErrorDialog(
                        $"Couldn't find local docker images, please ensure Docker is running.\n\n{error}"
                    )
            );

            UpdateUI();
        }
        #endregion

        #region Fns / Test
        private void OnLocalTestInputFocusIn(FocusInEvent evt)
        {
            OnLocalTestImageDropdownClick();
        }

        private void OnLocalTestInputsChanged(ChangeEvent<string> evt)
        {
            _localTestDeployBtn.SetEnabled(CheckFilledLocalTestInputs());
        }

        private async void OnLocalTestImageDropdownClick()
        {
            await GetLocalDockerImagesAsync();
            UnityEditor.PopupWindow.Show(
                _localTestImageShowDropdownBtn.worldBound,
                new CustomPopupContent(
                    _localImages,
                    OnDropdownLocalTestImageSelect,
                    _containerizeImageNameInputDefault
                )
            );
        }

        private void OnDropdownLocalTestImageSelect(string image)
        {
            _localTestImageInput.value = image;
            _localTestDockerRunInput.Focus();
        }

        private void OnLocalTestDockerParamsFocusOut(FocusOutEvent evt)
        {
            if (string.IsNullOrEmpty(_localTestDockerRunInput.value))
            {
                _localTestDockerRunInput.value = _localTestDockerRunInputDefault;
            }

            _localTestDeployBtn.SetEnabled(CheckFilledLocalTestInputs());
        }

        private bool CheckFilledLocalTestInputs()
        {
            return _localTestImageInput.value.Length > 0;
        }

        private async void OnLocalTestDeployClick()
        {
            try
            {
                hideResultLabels();

                if (IsLogLevelDebug)
                    Debug.Log("RunLocalImageAsync");

                string extraParams;

                if (_localTestDockerRunInput.value.Length > 0)
                {
                    if (_localTestDockerRunInput.value.Contains("-p "))
                    {
                        extraParams = _localTestDockerRunInput.value;
                    }
                    else
                    {
                        extraParams =
                            _localTestDockerRunInput.value + $" {_localTestDockerRunInputDefault}";
                    }
                }
                else
                {
                    extraParams = _localTestDockerRunInputDefault;
                }

                string img =
                    $"{_containerRegistryUrl}/{_containerProject}/{_localTestImageInput.value}";

                await EdgegapBuildUtils.RunCommand_DockerRun(img, extraParams);
                OnLocalDeploymentResult(
                    "Container deployed successfully. See more in Docker Desktop or Docker CLI.",
                    true
                );
                _createAppFoldout.SetValueWithoutNotify(true);
            }
            catch (Exception e)
            {
                string labelMsg;

                if (e.Message.Contains("Conflict"))
                {
                    labelMsg =
                        "Container already running. Make sure to terminate it before starting a new one.";
                }
                else
                {
                    labelMsg =
                        "There was an issue while deploying. See more in Docker Desktop or Docker CLI.";
                    Debug.LogError($"OnLocalTestDeploy Error: {e}");
                }

                OnLocalDeploymentResult(labelMsg, false);
            }
        }

        private async void OnLocalTestTerminateCLick()
        {
            try
            {
                hideResultLabels();

                if (IsLogLevelDebug)
                    Debug.Log("StopLocalImageAsync");

                await EdgegapBuildUtils.RunCommand_DockerStop();
                OnLocalDeploymentResult("Container terminated successfully.", true);

                _createAppFoldout.SetValueWithoutNotify(true);
            }
            catch (Exception e)
            {
                if (e.Message.Contains("No such container: edgegap-server-test"))
                {
                    OnLocalDeploymentResult("No deployment found.", true);
                }
                else
                {
                    Debug.LogError($"OnLocalTestTerminate Error: {e}");
                    OnLocalDeploymentResult(
                        "There was an issue while terminating. See more in Docker Desktop or Docker CLI.",
                        false
                    );
                }
            }
        }

        private void OnLocalDeploymentResult(string msg, bool success)
        {
            _localTestResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                msg,
                success
                    ? EdgegapWindowMetadata.StatusColors.Success
                    : EdgegapWindowMetadata.StatusColors.Error
            );
            _localTestResultLabel.style.display = DisplayStyle.Flex;
        }

        private void OnLocalContainerConnectLinkClick() =>
            OpenEdgegapDocsURL(EdgegapWindowMetadata.LOCAL_TEST_CONNECT_INFO_PATH);
        #endregion

        #region Fns / Upload App
        /// <summary>
        /// Show app name dropdown (Create App section) btn click
        /// </summary>
        private async void OnCreateAppNameDropdownClick()
        {
            try
            {
                _storedAppNames.Clear();
                await GetApps();
            }
            catch (Exception e)
            {
                ShowErrorDialog($"GetApps Error: {e}");
            }

            List<string> appNameBtns =
                !(_storedAppNames is null) || _storedAppNames.Count > 0
                    ? _storedAppNames
                        .Prepend(EdgegapWindowMetadata.DEFAULT_NEW_APPLICATION_LABEL)
                        .ToList()
                    : new List<string> { EdgegapWindowMetadata.DEFAULT_NEW_APPLICATION_LABEL };

            UnityEditor.PopupWindow.Show(
                _createAppNameShowDropdownBtn.worldBound,
                new CustomPopupContent(
                    appNameBtns,
                    OnDropdownCreateAppNameSelect,
                    _containerizeImageNameInputDefault
                )
            );
        }

        /// <summary>
        /// Select an app from the Create App section dropdown btn click
        /// </summary>
        /// <param name="name"></param>
        private void OnDropdownCreateAppNameSelect(string name)
        {
            string appName = Tokenize(name);
            _createAppNameInput.value = appName;
            _serverImageNameInput.Focus();
        }

        /// <summary>
        /// On change, validate
        /// Toggle create app btn if all required inputs are filled
        /// </summary>
        /// <param name="evt"></param>
        private void OnCreateAppNameInputFocusOut(FocusOutEvent evt)
        {
            // Validate: Only allow alphanumeric, underscore, dash, plus, period
            if (!_appNameAllowedCharsRegex.IsMatch(_createAppNameInput.value))
            {
                ShowErrorDialog(
                    "Your app name contains invalid characters. Only characters [a-zA-Z0-9_-+.] are allowed."
                );
            }

            _uploadImageCreateAppBtn.SetEnabled(CheckFilledCreateAppInputs());
        }

        /// <summary>
        /// On change, toggle create app btn if all required inputs in Create App section are filled
        /// </summary>
        /// <param name="evt"></param>
        private void OnCreateInputsChanged(ChangeEvent<string> evt)
        {
            _uploadImageCreateAppBtn.SetEnabled(CheckFilledCreateAppInputs());
        }

        private bool CheckFilledCreateAppInputs()
        {
            return _createAppNameInput.value.Length > 0
                && _serverImageNameInput.value.Length > 0
                && _serverImageTagInput.value.Length > 0;
        }

        private void OnPortsMappingLinkClick() =>
            OpenEdgegapDocsURL(EdgegapWindowMetadata.EDGEGAP_DOC_DEPLOY_GUIDE_PATH);

        /// <summary>
        /// "Upload image and Create app version" btn click
        /// Process UI + validation before/after API logic
        /// </summary>
        private async void OnUploadImageCreateAppBtnClickAsync()
        {
            try
            {
                _uploadImageCreateAppBtn.SetEnabled(false);
                hideResultLabels();

                // upload image
                if (IsLogLevelDebug)
                    Debug.Log("uploadDockerImageAsync");
                ProgressCounter = 0;

                string imageRepo = $"{_containerProject}/{_serverImageNameInput.value}";
                string tag = _serverImageTagInput.value;

                bool isDockerLoginSuccess = await EdgegapBuildUtils.LoginContainerRegistry(
                    _containerRegistryUrl,
                    _containerUsername,
                    _containerToken,
                    status => ShowWorkInProgress("Logging into container registry.", status)
                );

                if (!isDockerLoginSuccess)
                {
                    throw new Exception("Docker authorization failed (see logs).");
                }

                string pushError = await EdgegapBuildUtils.RunCommand_DockerPush(
                    _containerRegistryUrl,
                    imageRepo,
                    tag,
                    status => ShowWorkInProgress("Pushing Docker Image", status)
                );

                if (!string.IsNullOrEmpty(pushError.Trim()))
                {
                    Debug.LogError(pushError);
                    throw new Exception("Unable to push docker image to registry (see logs).");
                }

                ShowWorkInProgress("Create Application", "Updating server info on Edgegap");

                if (IsLogLevelDebug)
                    Debug.Log("createAppAsync");

                EdgegapAppApi appApi = getAppApi();

                EdgegapHttpResult<GetCreateAppResult> getAppResult = await appApi.GetApp(
                    _createAppNameInput.value
                );

                if (!getAppResult.IsResultCode200)
                {
                    CreateAppRequest createAppRequest = new CreateAppRequest(
                        _createAppNameInput.value,
                        isActive: true,
                        ""
                    );

                    EdgegapHttpResult<GetCreateAppResult> createAppResult = await appApi.CreateApp(
                        createAppRequest
                    );

                    if (!createAppResult.IsResultCode200)
                    {
                        Debug.LogError(createAppResult.HasErr);
                        throw new Exception(
                            $"Error {createAppResult.StatusCode}: {createAppResult.ReasonPhrase}"
                        );
                    }
                    else if (!_storedAppNames.Contains(_createAppNameInput.value))
                    {
                        _storedAppNames.Add(_createAppNameInput.value);
                    }
                }

                OpenEdgegapURL(
                    string.Join(
                        "",
                        new string[]
                        {
                            EdgegapWindowMetadata.EDGEGAP_CREATE_APP_BASE_URL,
                            _createAppNameInput.value,
                            "/versions/create/",
                            $"?name={HttpUtility.UrlEncode(_serverImageTagInput.value)}",
                            $"&imageRepo={HttpUtility.UrlEncode(imageRepo)}",
                            $"&dockerTag={HttpUtility.UrlEncode(tag)}",
                            $"&vCPU=1",
                            $"&memory=1",
                        }
                    )
                );
                ;

                _deployAppFoldout.SetValueWithoutNotify(true);
                _deployAppNameInput.SetValueWithoutNotify(_createAppNameInput.value);
            }
            catch (Exception e)
            {
                if (
                    e.Message.Contains("Docker authorization failed")
                    || e.Message.Contains("Unable to push docker image")
                )
                {
                    ShowErrorDialog(e.Message);
                }
                else
                {
                    Debug.LogError($"OnUploadImageCreateAppBtnClick Error: {e}");
                    ShowErrorDialog("Image upload and app creation failed (see logs).");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _uploadImageCreateAppBtn.SetEnabled(true);
            }
        }

        private EdgegapAppApi getAppApi() =>
            new EdgegapAppApi(
                EdgegapWindowMetadata.API_ENVIRONMENT,
                _apiToken,
                EdgegapWindowMetadata.LOG_LEVEL
            );

        private void OnBuildContainerizeUploadSuccess(Label displayLabel, string txt)
        {
            EditorUtility.ClearProgressBar();

            displayLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                txt,
                EdgegapWindowMetadata.StatusColors.Success
            );
            displayLabel.visible = true;
        }

        private void OnYourAppLinkClick() =>
            OpenEdgegapDocsURL(EdgegapWindowMetadata.EDGEGAP_DOC_APP_INFO_PATH);
        #endregion

        #region Fns / Deploy
        private void OnDeployAppNameInputFocusIn(FocusInEvent evt)
        {
            OnDeployAppNameDropdownClick();
        }

        /// <summary>
        /// Show app name dropdown (Deploy section) btn click
        /// </summary>
        private async void OnDeployAppNameDropdownClick()
        {
            try
            {
                _storedAppNames.Clear();
                await GetApps();
            }
            catch (Exception e)
            {
                ShowErrorDialog($"GetApps Error: {e}");
            }

            UnityEditor.PopupWindow.Show(
                _deployAppNameShowDropdownBtn.worldBound,
                new CustomPopupContent(
                    _storedAppNames,
                    OnDropdownDeployAppNameSelect,
                    _containerizeImageNameInputDefault
                )
            );
        }

        /// <summary>
        /// Select an app from the Deploy section dropdown btn click
        /// </summary>
        /// <param name="name"></param>
        private void OnDropdownDeployAppNameSelect(string name)
        {
            string appName = Regex.Replace(name, @"\s", "_");
            _deployAppNameInput.value = appName;
            _deployAppVersionShowDropdownBtn.Focus();
        }

        /// <summary>
        /// On change, validate
        /// Toggle deploy app btn if all required inputs in Deploy App section are filled
        /// </summary>
        /// <param name="evt"></param>
        private void OnDeployAppNameInputChanged(ChangeEvent<string> evt)
        {
            DeployAppNameInputChanged(evt.newValue);
        }

        private async void DeployAppNameInputChanged(string newValue)
        {
            _deployAppBtn.SetEnabled(CheckFilledDeployServerInputs());
            _deployAppVersionShowDropdownBtn.SetEnabled(false);
            try
            {
                if (_storedAppVersions is not null)
                {
                    _storedAppVersions.Clear();
                }

                if (_storedAppNames is not null && _storedAppNames.Contains(newValue))
                {
                    await GetAppVersions();
                }
            }
            catch (Exception e)
            {
                ShowErrorDialog($"GetAppVersions Error: {e}");
            }
            finally
            {
                if (_storedAppVersions is not null && _storedAppVersions.Count > 0)
                {
                    _deployAppBtn.SetEnabled(CheckFilledDeployServerInputs());
                    _deployAppVersionShowDropdownBtn.SetEnabled(true);
                }
            }
        }

        private void OnDeployAppVersionInputFocusIn(FocusInEvent evt)
        {
            OnDeployAppVersionDropdownClick();
        }

        /// <summary>
        /// Show app version dropdown btn click
        /// </summary>
        private async void OnDeployAppVersionDropdownClick()
        {
            if (string.IsNullOrEmpty(_deployAppNameInput.value.Trim()))
                return;

            try
            {
                _storedAppVersions?.Clear();
                await GetAppVersions();
            }
            catch (Exception e)
            {
                ShowErrorDialog($"GetAppVersions Error: {e}");
            }

            UnityEditor.PopupWindow.Show(
                _deployAppVersionShowDropdownBtn.worldBound,
                new CustomPopupContent(_storedAppVersions, OnDropDownDeployAppVersionSelect, "")
            );
        }

        /// <summary>
        /// Select an app version from the Deploy section dropdown btn click
        /// </summary>
        /// <param name="version"></param>
        private void OnDropDownDeployAppVersionSelect(string version)
        {
            _deployAppVersionInput.value = version;
            _deployAppBtn.Focus();
        }

        /// <summary>
        /// On change, toggle deploy app btn if all required inputs in Deploy App section are filled
        /// </summary>
        /// <param name="evt"></param>
        private void OnDeployAppVersionInputChanged(ChangeEvent<string> evt)
        {
            _deployAppBtn.SetEnabled(CheckFilledDeployServerInputs());
        }

        private void OnDeployLimitLinkClick() =>
            OpenEdgegapURL(EdgegapWindowMetadata.EDGEGAP_FREE_TIER_INFO_URL);

        /// <summary>
        /// "Deploy to cloud" btn click
        /// Process UI + validation before/after API logic
        /// </summary>
        private async void OnDeploymentCreateBtnClick()
        {
            try
            {
                hideResultLabels();

                await CreateDeploymentStartServer();

                _nextStepsFoldout.SetValueWithoutNotify(true);
            }
            catch (Exception e)
            {
                OnCreateDeploymentStartServerFail(e.Message);
            }
        }

        /// <summary>
        /// Starts a new deployment & waits for it to be READY
        /// </summary>
        private async Task CreateDeploymentStartServer()
        {
            if (IsLogLevelDebug)
                Debug.Log("createDeploymentStartServerAsync");

            _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                EdgegapWindowMetadata.DEPLOY_REQUEST_RICH_STR,
                EdgegapWindowMetadata.StatusColors.Processing
            );
            _deployResultLabel.style.display = DisplayStyle.Flex;

            EdgegapDeploymentsApi deployApi = GetDeployAPI();

            // Get (+cache) external IP async, required to create a deployment. Prioritize cache.
            if (string.IsNullOrEmpty(_userExternalIp))
            {
                EdgegapIpApi ipApi = new EdgegapIpApi(
                    EdgegapWindowMetadata.API_ENVIRONMENT,
                    _apiToken,
                    EdgegapWindowMetadata.LOG_LEVEL
                );
                EdgegapHttpResult<GetYourPublicIpResult> getYourPublicIpResponseTask =
                    await ipApi.GetYourPublicIp();

                _userExternalIp = getYourPublicIpResponseTask?.Data?.PublicIp;
                if (!string.IsNullOrEmpty(_userExternalIp))
                {
                    Debug.LogWarning(
                        $"Couldn't retrieve your public IP. {getYourPublicIpResponseTask.Error}"
                    );
                }
            }

            CreateDeploymentRequest createDeploymentReq = new CreateDeploymentRequest( // MIRROR CHANGE: 'new()' not supported in Unity 2020
                _deployAppNameInput.value,
                _deployAppVersionInput.value,
                _userExternalIp
            );

            // Request to deploy (it won't be active, yet) =>
            EdgegapHttpResult<CreateDeploymentResult> createDeploymentResponse =
                await deployApi.CreateDeploymentAsync(createDeploymentReq);
            if (!createDeploymentResponse.IsResultCode200)
            {
                OnCreateDeploymentStartServerFail(createDeploymentResponse.Error.ErrorMessage);
                return;
            }
            else
            {
                // Update status
                _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                    "Deployment starting, see Dashboard for details.",
                    EdgegapWindowMetadata.StatusColors.Processing
                );

                OpenEdgegapURL(EdgegapWindowMetadata.EDGEGAP_DEPLOY_APP_URL);
            }

            // Check the status of the deployment for READY every 2s =>
            const int pollIntervalSecs = EdgegapWindowMetadata.DEPLOYMENT_READY_STATUS_POLL_SECONDS;
            EdgegapHttpResult<GetDeploymentStatusResult> getDeploymentStatusResponse =
                await deployApi.AwaitReadyStatusAsync(
                    createDeploymentResponse.Data.RequestId,
                    TimeSpan.FromSeconds(pollIntervalSecs)
                );

            // Process create deployment response
            if (string.IsNullOrEmpty(getDeploymentStatusResponse?.Error?.ErrorMessage))
            {
                _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                    "Server deployed successfully. Don't forget to remove the deployment after testing.",
                    EdgegapWindowMetadata.StatusColors.Success
                );
                _deployResultLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                OnCreateDeploymentStartServerFail(getDeploymentStatusResponse.Error.ErrorMessage);
            }
        }

        /// <summary>
        /// CreateDeployment fail handler.
        /// </summary>
        /// <param name="reachedNumDeploymentsHardcap">if maximum number of deployments was reached</param>
        /// <param name="message">error message to log</param>
        private void OnCreateDeploymentStartServerFail(string message = null)
        {
            ShowErrorDialog(
                message ?? "Unknown Error, see Unity Console.",
                _deployResultLabel,
                "There was an issue, see Unity console for details."
            );
            Debug.Log(
                "See deployments on Dashboard: https://app.edgegap.com/deployment-management/deployments/list"
            );
        }

        /// <summary>
        /// "Stop last deployment" btn click
        /// Process UI + validation before/after API logic
        /// </summary>
        private async void OnStopLastDeployClick()
        {
            try
            {
                hideResultLabels();

                if (IsLogLevelDebug)
                    Debug.Log("GetStopLastDeploymentAsync");

                string _deploymentRequestId;
                EdgegapDeploymentsApi deployApi = GetDeployAPI();

                if (IsLogLevelDebug)
                    Debug.Log("GetQuickstartDeploymentsAsync");

                List<string> quickstartDeploymentIds = await GetQuickstartDeployments();

                if (quickstartDeploymentIds.Count > 0)
                {
                    _deploymentRequestId = quickstartDeploymentIds[0];
                }
                else
                {
                    OnGetStopLastDeploymentResult("No quickstart deployment found", true);
                    return;
                }

                //Stop request
                EdgegapHttpResult<StopActiveDeploymentResult> stopDeploymentResponse =
                    await deployApi.StopActiveDeploymentAsync(_deploymentRequestId);

                _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                    "Stopping...",
                    EdgegapWindowMetadata.StatusColors.Warn
                );
                _deployResultLabel.style.display = DisplayStyle.Flex;

                if (!stopDeploymentResponse.IsResultCode200)
                {
                    OnGetStopLastDeploymentResult(stopDeploymentResponse.Error.ErrorMessage, false);
                    return;
                }

                //Check status of deployment for STOPPED every 2s
                TimeSpan pollIntervalSecs = TimeSpan.FromSeconds(
                    EdgegapWindowMetadata.DEPLOYMENT_STOP_STATUS_POLL_SECONDS
                );
                stopDeploymentResponse = await deployApi.AwaitTerminatedDeleteStatusAsync(
                    _deploymentRequestId,
                    pollIntervalSecs
                );

                //Process response
                if (!stopDeploymentResponse.IsResultCode410)
                {
                    OnGetStopLastDeploymentResult(stopDeploymentResponse.Error.ErrorMessage, false);
                }
                else
                {
                    OnGetStopLastDeploymentResult("Deployment stopped successfully", true);
                }

                _nextStepsFoldout.SetValueWithoutNotify(true);
            }
            catch (Exception e)
            {
                OnGetStopLastDeploymentResult(e.Message, false);
                OpenEdgegapURL(EdgegapWindowMetadata.EDGEGAP_DEPLOY_APP_URL);
            }
        }

        private void OnGetStopLastDeploymentResult(string msg, bool success)
        {
            _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                msg,
                success
                    ? EdgegapWindowMetadata.StatusColors.Success
                    : EdgegapWindowMetadata.StatusColors.Error
            );
            _deployResultLabel.style.display = DisplayStyle.Flex;
        }

        private bool CheckFilledDeployServerInputs()
        {
            return _deployAppNameInput.value.Trim().Length > 0
                && _deployAppVersionInput.value.Trim().Length > 0;
        }

        private async Task<List<string>> GetQuickstartDeployments()
        {
            EdgegapDeploymentsApi deployApi = GetDeployAPI();

            EdgegapHttpResult<GetDeploymentsResult> getDeploymentsResponse =
                await deployApi.GetDeploymentsAsync();

            if (!getDeploymentsResponse.IsResultCode200)
            {
                return new List<string>();
            }

            List<GetDeploymentResult> quickstartDeploys = getDeploymentsResponse
                .Data.Data.Where(deploy =>
                    deploy.Tags is not null
                    && deploy.Tags.Contains(EdgegapWindowMetadata.DEFAULT_DEPLOYMENT_TAG)
                )
                .ToList();
            return quickstartDeploys.Select(deploy => deploy.RequestId).ToList();
        }
        #endregion

        #region Fns / Next
        private void OnServerConnectLinkClick() =>
            OpenEdgegapDocsURL(EdgegapWindowMetadata.CONNECT_TO_DEPLOYMENT_INFO_URL);

        private void OnGen2MatchmakerLinkClick() =>
            OpenEdgegapDocsURL(EdgegapWindowMetadata.EDGEGAP_DOC_MANAGED_MATCHMAKER_PATH);

        private void OnAdvMatchmakerLinkClick() =>
            OpenEdgegapDocsURL(EdgegapWindowMetadata.EDGEGAP_DOC_ADV_MATCHMAKER_PATH);

        private void OnScalingLifecycleLinkClick() =>
            OpenEdgegapDocsURL(EdgegapWindowMetadata.SCALING_LIFECYCLE_INFO_URL);
        #endregion

        #region Fns / Discord
        private void OnDiscordBtnClick() =>
            Application.OpenURL(EdgegapWindowMetadata.EDGEGAP_DISCORD_URL);
        #endregion

        #region State Management
        private void ShowErrorDialog(
            string dialogMsg,
            Label displayLabel = null,
            string labelTxt = null
        )
        {
            EditorUtility.ClearProgressBar();

            if (displayLabel is not null && !string.IsNullOrEmpty(labelTxt))
            {
                displayLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                    labelTxt,
                    EdgegapWindowMetadata.StatusColors.Error
                );
                displayLabel.visible = true;
            }

            if (!string.IsNullOrEmpty(dialogMsg))
            {
                EditorUtility.DisplayDialog("Error", dialogMsg, "Ok");
            }
        }

        private void ShowWorkInProgress(string title, string status)
        {
            EditorUtility.DisplayProgressBar(title, status, ProgressCounter++ / 50);
        }

        private async Task InitializeState()
        {
            if (string.IsNullOrEmpty(_apiToken))
            {
                //show Sign In btn
                ToggleIsConnectedContainers(false);
                return;
            }
            else
            {
                //show API Token field/btns + Sign Out btn
                ToggleIsConnectedContainers(true);
            }

            if (IsLogLevelDebug)
                Debug.Log(
                    "syncFormWithObjectDynamicAsync: Found apiToken; "
                        + "calling verifyApiTokenGetRegistryCredsAsync =>"
                );
            await verifyApiTokenGetRegistryCreds();

            if (_isApiTokenVerified)
            {
                if (IsLogLevelDebug)
                    Debug.Log("syncFormWithObjectDynamicAsync: Found apiToken;");

                _signOutBtn.SetEnabled(false);

                _createAppNameShowDropdownBtn.SetEnabled(false);
                _deployAppNameShowDropdownBtn.SetEnabled(false);

                try
                {
                    if (IsLogLevelDebug)
                        Debug.Log("GetAppsAsync");

                    await GetApps();
                }
                finally
                {
                    _createAppNameShowDropdownBtn.SetEnabled(true);

                    if (_storedAppNames is not null && _storedAppNames.Count > 0)
                    {
                        _deployAppNameShowDropdownBtn.SetEnabled(true);
                    }
                }

                if (IsLogLevelDebug)
                    Debug.Log(
                        "syncFormWithObjectDynamicAsync: Found apiToken; "
                            + "calling GetLocalDockerImagesAsync =>"
                    );

                _localTestImageShowDropdownBtn.SetEnabled(false);

                try
                {
                    await GetLocalDockerImagesAsync();
                }
                finally
                {
                    if (_localImages is not null && _localImages.Count > 0)
                    {
                        _localTestImageShowDropdownBtn.SetEnabled(true);
                    }
                }

                _signOutBtn.SetEnabled(true);
                UpdateUI();
            }

            // Was the API token verified + we found a cached appName in Deploy section? Load the app versions for the dropdown =>
            // But ignore errs, since we're just *assuming* the app exists since the appName was filled
            if (_isApiTokenVerified && _deployAppNameInput.value.Length > 0)
            {
                if (IsLogLevelDebug)
                    Debug.Log(
                        "syncFormWithObjectDynamicAsync: Found apiToken && deployAppName value; "
                            + "calling GetAppVersionsAsync =>"
                    );

                _signOutBtn.SetEnabled(false);
                _deployAppVersionShowDropdownBtn.SetEnabled(false);

                try
                {
                    await GetAppVersions();
                }
                finally
                {
                    if (_storedAppVersions is not null && _storedAppVersions.Count > 0)
                    {
                        _deployAppVersionShowDropdownBtn.SetEnabled(true);
                    }
                }

                _signOutBtn.SetEnabled(true);
            }

            // Was the API token verified + found stored deployment ID?
            // refresh to see if it's still running
            if (_isApiTokenVerified)
            {
                if (IsLogLevelDebug)
                    Debug.Log(
                        "syncFormWithObjectDynamicAsync: Found apiToken && _deploymentRequestId; "
                            + "calling RefreshDeploymentsAsync =>"
                    );
            }
        }

        /// <summary>
        /// Toggle container groups and foldouts on/off based on:
        /// - _isApiTokenVerified
        /// </summary>
        private void UpdateUI()
        {
            _postAuthContainer.SetEnabled(_isApiTokenVerified); // Entire body container
            _serverBuildFoldout.SetEnabled(_isApiTokenVerified);
            _containerizeFoldout.SetEnabled(_isApiTokenVerified);
            _localTestFoldout.SetEnabled(_isApiTokenVerified);
            _createAppFoldout.SetEnabled(_isApiTokenVerified);
            _deployAppFoldout.SetEnabled(_isApiTokenVerified);
            _nextStepsFoldout.SetEnabled(_isApiTokenVerified);

            if (!_isApiTokenVerified)
            {
                _serverBuildFoldout.SetValueWithoutNotify(false);
                _containerizeFoldout.SetValueWithoutNotify(false);
                _localTestFoldout.SetValueWithoutNotify(false);
                _createAppFoldout.SetValueWithoutNotify(false);
                _deployAppFoldout.SetValueWithoutNotify(false);
                _nextStepsFoldout.SetValueWithoutNotify(false);
            }
            else
            {
                _serverBuildFoldout.SetValueWithoutNotify(true);

                // Set default values if empty fields
                TogglePlaceholder(_buildFolderNameInput, _buildFolderNameInputDefault, false);
                TogglePlaceholder(_buildPathInput, _buildPathInputDefault, false);
                TogglePlaceholder(
                    _containerizeImageNameInput,
                    _containerizeImageNameInputDefault,
                    false
                );
                TogglePlaceholder(
                    _containerizeImageTagInput,
                    _containerizeImageTagInputDefault,
                    false
                );
                TogglePlaceholder(_dockerfilePathInput, _dockerfilePathInputDefault, false);
                if (_localImages is not null && _localImages.Count > 0)
                {
                    TogglePlaceholder(_localTestImageInput, _localImages[0], false);
                    string[] localImageAndVersion = _localImages[0].Split(":");

                    if (_storedAppNames is null || _storedAppNames.Count == 0)
                    {
                        _createAppNameInput.SetValueWithoutNotify(localImageAndVersion[0]);
                        _deployAppNameInput.SetValueWithoutNotify("");
                        _deployAppVersionInput.SetValueWithoutNotify("");
                    }
                    TogglePlaceholder(_serverImageNameInput, localImageAndVersion[0], false);
                    TogglePlaceholder(_serverImageTagInput, localImageAndVersion[1], false);
                }
                else
                {
                    _localTestImageInput.SetValueWithoutNotify("");
                    _createAppNameInput.SetValueWithoutNotify("");
                    _serverImageNameInput.SetValueWithoutNotify("");
                    _serverImageTagInput.SetValueWithoutNotify("");
                }
                TogglePlaceholder(_localTestDockerRunInput, _localTestDockerRunInputDefault, false);

                if (_storedAppNames is not null && _storedAppNames.Count == 1)
                {
                    OnDropdownCreateAppNameSelect(_storedAppNames[0]);
                    TogglePlaceholder(_deployAppNameInput, _storedAppNames[0], false);
                }

                //open other foldouts if (non-default) persistent data is found in inputs
                if (
                    _buildPathInput.value.Trim().Length > 0
                    || _containerizeImageNameInput.value.Trim().Length > 0
                    || _containerizeImageTagInput.value.Trim().Length > 0
                    || (
                        _dockerfilePathInput.value.Trim().Length > 0
                        && _dockerfilePathInput.value != _dockerfilePathInputDefault
                    )
                    || _optionalDockerParamsInput.value.Trim().Length > 0
                )
                    _containerizeFoldout.SetValueWithoutNotify(true);

                if (
                    _localTestImageInput.value.Trim().Length > 0
                    || (
                        _localTestDockerRunInput.value.Trim().Length > 0
                        && _localTestDockerRunInput.value != _localTestDockerRunInputDefault
                    )
                )
                    _localTestFoldout.SetValueWithoutNotify(true);

                if (
                    _createAppNameInput.value.Trim().Length > 0
                    || _serverImageNameInput.value.Trim().Length > 0
                    || _serverImageTagInput.value.Trim().Length > 0
                )
                    _createAppFoldout.SetValueWithoutNotify(true);

                if (
                    _deployAppNameInput.value.Trim().Length > 0
                    || _deployAppVersionInput.value.Trim().Length > 0
                )
                    _deployAppFoldout.SetValueWithoutNotify(true);
            }
        }

        private async Task GetApps()
        {
            EdgegapAppApi appApi = getAppApi();
            EdgegapHttpResult<GetAppsResult> getAppsResult = await appApi.GetApps();

            if (getAppsResult.IsResultCode200)
            {
                GetAppsResult existingApps = getAppsResult.Data;
                _storedAppNames = existingApps.Applications.Select(app => app.AppName).ToList();

                if (!_storedAppNames.Contains(_deployAppNameInput.value))
                {
                    // select if only one option, otherwise clear
                    if (_storedAppNames.Count == 1)
                    {
                        _deployAppNameInput.value = _storedAppNames[0];
                    }
                    else
                    {
                        _deployAppNameInput.value = "";
                    }
                }
            }
            else
            {
                Debug.LogWarning(
                    $"Unable to retrieve applications.\n"
                        + $"Status {getAppsResult.StatusCode}: {getAppsResult.ReasonPhrase}"
                );
            }
        }

        private async Task GetAppVersions()
        {
            if (IsLogLevelDebug)
                Debug.Log("GetAppVersions");

            EdgegapAppApi appApi = getAppApi();
            EdgegapHttpResult<GetAppVersionsResult> getAppVersionsResult =
                await appApi.GetAppVersions(_deployAppNameInput.value);

            if (getAppVersionsResult.IsResultCode200)
            {
                GetAppVersionsResult appVersionsData = getAppVersionsResult.Data;

                List<VersionData> activeVersions = appVersionsData
                    .Versions.Where(version => version.IsActive)
                    .ToList();
                _storedAppVersions = activeVersions.Select(version => version.Name).ToList();

                // select if only one option, otherwise clear
                if (_storedAppVersions.Count == 1)
                {
                    OnDropDownDeployAppVersionSelect(_storedAppVersions[0]);
                }
                else
                {
                    OnDropDownDeployAppVersionSelect("");
                }
            }
            else
            {
                Debug.LogWarning(
                    $"Unable to retrieve app versions for application {_deployAppNameInput.value}.\n"
                        + $"Status {getAppVersionsResult.StatusCode}: {getAppVersionsResult.ReasonPhrase}"
                );
            }
        }

        private EdgegapDeploymentsApi GetDeployAPI()
        {
            if (_deployAPI is null)
            {
                _deployAPI = new EdgegapDeploymentsApi(
                    EdgegapWindowMetadata.API_ENVIRONMENT,
                    _apiToken,
                    EdgegapWindowMetadata.LOG_LEVEL
                );
            }
            return _deployAPI;
        }
        #endregion

        #region Utility / TextField
        // <summary>
        // Toggle specified placeholder value, with the option to force state.
        /// <param name="input">TextField element to toggle state on.</param>
        /// <param name="placeholder">Placeholder value to use.</param>
        /// <param name="focus">Input focused or not.</param>
        // </summary>
        private void TogglePlaceholder(TextField input, string placeholder, bool focus)
        {
            // if custom non-empty value provided, disable placeholder class and do nothing
            if (input.value.ToString() != placeholder && !string.IsNullOrEmpty(input.value.Trim()))
            {
                return;
            }

            if (focus)
            {
                input.SetValueWithoutNotify("");
            }
            else
            {
                input.SetValueWithoutNotify(placeholder);
            }
        }

        private string Tokenize(string input)
        {
            return Regex.Replace(input.Trim(), @"[\W]+", "-");
        }
        #endregion

        #region Utility / Browser
        private void OpenEdgegapURL(string URL) =>
            Application.OpenURL(
                $"{URL}{(URL.Contains("?") ? "&" : "?")}{EdgegapWindowMetadata.DEFAULT_UTM_TAGS}"
            );

        private void OpenEdgegapDocsURL(string path) =>
            // TODO: append "?{EdgegapWindowMetadata.DEFAULT_UTM_TAGS}"
            Application.OpenURL($"{EdgegapWindowMetadata.EDGEGAP_DOC_BASE_URL}{path}");
        #endregion

        #region Utility / HTTP
        public static string Base64Encode(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainBytes);
        }

        public static string Base64Decode(string base64EncodedText)
        {
            byte[] base64Bytes = Convert.FromBase64String(base64EncodedText);
            return Encoding.UTF8.GetString(base64Bytes);
        }
        #endregion
    }
}

#endif
#endif
