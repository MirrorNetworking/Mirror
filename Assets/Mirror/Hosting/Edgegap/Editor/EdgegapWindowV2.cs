using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Edgegap.Editor.Api;
using Edgegap.Editor.Api.Models;
using Edgegap.Editor.Api.Models.Requests;
using Edgegap.Editor.Api.Models.Results;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using Application = UnityEngine.Application;

namespace Edgegap.Editor
{
    /// <summary>
    /// Editor logic event handler for "UI Builder" EdgegapWindow.uxml, superceding` EdgegapWindow.cs`.
    /// </summary>
    public class EdgegapWindowV2 : EditorWindow
    {
        #region Vars
        public static bool IsLogLevelDebug =>
            EdgegapWindowMetadata.LOG_LEVEL == EdgegapWindowMetadata.LogLevel.Debug;
        private bool IsInitd;
        private VisualTreeAsset _visualTree;
        private bool _isApiTokenVerified; // Toggles the rest of the UI
        private bool _isContainerRegistryReady;
        private Sprite _appIconSpriteObj;
        private string _appIconBase64Str;
 #pragma warning disable CS0414 // MIRROR CHANGE: hide unused warning
        private ApiEnvironment _apiEnvironment; // TODO: Swap out hard-coding with UI element?
 #pragma warning restore CS0414 // END MIRROR CHANGE
        private GetRegistryCredentialsResult _credentials;
        private static readonly Regex _appNameAllowedCharsRegex = new Regex(@"^[a-zA-Z0-9_\-+\.]*$"); // MIRROR CHANGE: 'new()' not supported in Unity 2020
        private GetCreateAppResult _loadedApp;
        /// <summary>TODO: Make this a list</summary>
        private GetDeploymentStatusResult _lastKnownDeployment;
        private string _deploymentRequestId;
        private string _userExternalIp;
        private bool _isAwaitingDeploymentReadyStatus;
        #endregion // Vars


        #region Vars -> Interactable Elements
        private Button _debugBtn;

        /// <summary>(!) This is saved manually to EditorPrefs via Base64 instead of via UiBuilder</summary>
        private TextField _apiTokenInput;

        private Button _apiTokenVerifyBtn;
        private Button _apiTokenGetBtn;
        private VisualElement _postAuthContainer;

        private Foldout _appInfoFoldout;
        private Button _appLoadExistingBtn;
        private TextField _appNameInput;
        /// <summary>`Sprite` type</summary>
        private ObjectField _appIconSpriteObjInput;
        private Button _appCreateBtn;
        private Label _appCreateResultLabel;

        private Foldout _containerRegistryFoldout;
        private TextField _containerNewTagVersionInput;
        private TextField _containerPortNumInput;

        // MIRROR CHANGE: EnumField Port type fails to resolve unless in Assembly-CSharp-Editor.dll. replace with regular Dropdown instead.
        /// <summary>`ProtocolType` type</summary>
        // private EnumField _containerTransportTypeEnumInput;
        private PopupField<string> _containerTransportTypeEnumInput;
        // END MIRROR CHANGE

        private Toggle _containerUseCustomRegistryToggle;
        private VisualElement _containerCustomRegistryWrapper;
        private TextField _containerRegistryUrlInput;
        private TextField _containerImageRepositoryInput;
        private TextField _containerUsernameInput;
        private TextField _containerTokenInput;
        private Button _containerBuildAndPushServerBtn;
        private Label _containerBuildAndPushResultLabel;

        private Foldout _deploymentsFoldout;
        private Button _deploymentsRefreshBtn;
        private Button _deploymentsCreateBtn;
        /// <summary>display:none (since it's on its own line), rather than !visible.</summary>
        private Label _deploymentsStatusLabel;
        private VisualElement _deploymentsServerDataContainer;
        private Button _deploymentConnectionCopyUrlBtn;
        private TextField _deploymentsConnectionUrlReadonlyInput;
        private Label _deploymentsConnectionStatusLabel;
        private Button _deploymentsConnectionStopBtn;

        private Button _footerDocumentationBtn;
        private Button _footerNeedMoreGameServersBtn;
        #endregion // Vars

        // MIRROR CHANGE
        // get the path of this .cs file so we don't need to hardcode paths to
        // the .uxml and .uss files:
        // https://forum.unity.com/threads/too-many-hard-coded-paths-in-the-templates-and-documentation.728138/
        // this way users can move this folder without breaking UIToolkit paths.
        internal string StylesheetPath =>
            Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
        // END MIRROR CHANGE

        // MIRROR CHANGE: images are dragged into the script in inspector and assigned to the UI at runtime. this way we don't need to hardcode it.
        public Texture2D LogoImage;
        public Texture2D ClipboardImage;
        // END MIRROR CHANGE

        [MenuItem("Edgegap/Edgegap Hosting")] // MIRROR CHANGE: more obvious title
        public static void ShowEdgegapToolWindow()
        {
            EdgegapWindowV2 window = GetWindow<EdgegapWindowV2>();
            window.titleContent = new GUIContent("Edgegap Hosting"); // MIRROR CHANGE: 'Edgegap Server Management' is too long for the tab space
            window.maxSize = new Vector2(635, 900);
            window.minSize = window.maxSize;
        }


        #region Unity Funcs
        protected void OnEnable()
        {
#if UNITY_2021_3_OR_NEWER // MIRROR CHANGE: only load stylesheet in supported Unity versions, otherwise it shows errors in U2020
            // Set root VisualElement and style: V2 still uses EdgegapWindow.[uxml|uss]
            // BEGIN MIRROR CHANGE
            _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{StylesheetPath}/EdgegapWindow.uxml");
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{StylesheetPath}/EdgegapWindow.uss");
            // END MIRROR CHANGE
            rootVisualElement.styleSheets.Add(styleSheet);
#endif
        }

#pragma warning disable CS1998 // MIRROR CHANGE: disable async warning in U2020
        public async void CreateGUI()
#pragma warning restore CS1998 // END MIRROR CHANGE
        {
            // MIRROR CHANGE: the UI requires 'GroupBox', which is not available in Unity 2019/2020.
            // showing it will break all of Unity's Editor UIs, not just this one.
            // instead, show a warning that the Edgegap plugin only works on Unity 2021+
#if !UNITY_2021_3_OR_NEWER
            Debug.LogWarning("The Edgegap Hosting plugin requires UIToolkit in Unity 2021.3 or newer. Please upgrade your Unity version to use this.");
#else
            // Get UI elements from UI Builder
            rootVisualElement.Clear();
            _visualTree.CloneTree(rootVisualElement);

            // Register callbacks and sync UI builder elements to fields here
            InitUIElements();
            syncFormWithObjectStatic();
            await syncFormWithObjectDynamicAsync(); // API calls

            IsInitd = true;
#endif
        }

        /// <summary>The user closed the window. Save the data.</summary>
        protected void OnDisable()
        {
#if UNITY_2021_3_OR_NEWER // MIRROR CHANGE: only load stylesheet in supported Unity versions, otherwise it shows errors in U2020
            // MIRROR CHANGE: sometimes this is called without having been registered, throwing NRE
            if (_debugBtn == null) return;
            // END MIRROR CHANGE

            unregisterClickEvents();
            unregisterFieldCallbacks();
            SyncObjectWithForm();
#endif
        }
        #endregion // Unity Funcs


        #region Init
        /// <summary>
        /// Binds the form inputs to the associated variables and initializes the inputs as required.
        /// Requires the VisualElements to be loaded before this call. Otherwise, the elements cannot be found.
        /// </summary>
        private void InitUIElements()
        {
            setVisualElementsToFields();
            assertVisualElementKeys();
            closeDisableGroups();
            registerClickCallbacks();
            registerFieldCallbacks();
            initToggleDynamicUi();
            AssignImages(); // MIRROR CHANGE
        }

        private void closeDisableGroups()
        {
            _appInfoFoldout.value = false;
            _containerRegistryFoldout.value = false;
            _deploymentsFoldout.value = false;

            _appInfoFoldout.SetEnabled(false);
            _containerRegistryFoldout.SetEnabled(false);
            _deploymentsFoldout.SetEnabled(false);
        }

        // MIRROR CHANGE: assign images to the UI at runtime instead of hardcoding it
        void AssignImages()
        {
            // header logo
            VisualElement logoElement = rootVisualElement.Q<VisualElement>("header-logo-img");
            logoElement.style.backgroundImage = LogoImage;

            // clipboard button
            VisualElement copyElement = rootVisualElement.Q<VisualElement>("DeploymentConnectionCopyUrlBtn");
            copyElement.style.backgroundImage = ClipboardImage;
        }

        // END MIRROR CHANGE

        /// <summary>Set fields referencing UI Builder's fields. In order of appearance from top-to-bottom.</summary>
        private void setVisualElementsToFields()
        {
            _debugBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEBUG_BTN_ID);

            _apiTokenInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.API_TOKEN_TXT_ID);
            _apiTokenVerifyBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.API_TOKEN_VERIFY_BTN_ID);
            _apiTokenGetBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.API_TOKEN_GET_BTN_ID);
            _postAuthContainer = rootVisualElement.Q<VisualElement>(EdgegapWindowMetadata.POST_AUTH_CONTAINER_ID);

            _appInfoFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.APP_INFO_FOLDOUT_ID);
            _appNameInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.APP_NAME_TXT_ID);
            _appLoadExistingBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.APP_LOAD_EXISTING_BTN_ID);
            _appIconSpriteObjInput = rootVisualElement.Q<ObjectField>(EdgegapWindowMetadata.APP_ICON_SPRITE_OBJ_ID);
            _appCreateBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.APP_CREATE_BTN_ID);
            _appCreateResultLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.APP_CREATE_RESULT_LABEL_ID);

            _containerRegistryFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.CONTAINER_REGISTRY_FOLDOUT_ID);
            _containerNewTagVersionInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINER_NEW_TAG_VERSION_TXT_ID);
            _containerPortNumInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINER_REGISTRY_PORT_NUM_ID);
            // MIRROR CHANGE: dynamically resolving PortType fails if not in Assembly-CSharp-Editor.dll. Hardcode UDP/TCP instead.
            // this finds the placeholder and dynamically replaces it with a popup field
            VisualElement dropdownPlaceholder = rootVisualElement.Q<VisualElement>("MIRROR_CHANGE_PORT_HARDCODED");
            List<string> options = new List<string> { "UDP", "TCP" };
            _containerTransportTypeEnumInput = new PopupField<string>("Protocol Type", options, 0);
            dropdownPlaceholder.Add(_containerTransportTypeEnumInput);
            // END MIRROR CHANGE
            _containerUseCustomRegistryToggle = rootVisualElement.Q<Toggle>(EdgegapWindowMetadata.CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID);
            _containerCustomRegistryWrapper = rootVisualElement.Q<VisualElement>(EdgegapWindowMetadata.CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID);
            _containerRegistryUrlInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINER_REGISTRY_URL_TXT_ID);
            _containerImageRepositoryInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID);
            _containerUsernameInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINER_USERNAME_TXT_ID);
            _containerTokenInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINER_TOKEN_TXT_ID);
            _containerBuildAndPushServerBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.CONTAINER_BUILD_AND_PUSH_BTN_ID);
            _containerBuildAndPushResultLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.CONTAINER_BUILD_AND_PUSH_RESULT_LABEL_ID);

            _deploymentsFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.DEPLOYMENTS_FOLDOUT_ID);
            _deploymentsRefreshBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOYMENTS_REFRESH_BTN_ID);
            _deploymentsCreateBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOYMENTS_CREATE_BTN_ID);
            _deploymentsStatusLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.DEPLOYMENTS_STATUS_LABEL_ID);
            _deploymentsServerDataContainer = rootVisualElement.Q<VisualElement>(EdgegapWindowMetadata.DEPLOYMENTS_CONTAINER_ID); // Dynamic
            _deploymentConnectionCopyUrlBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_COPY_URL_BTN_ID);
            _deploymentsConnectionUrlReadonlyInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_URL_READONLY_TXT_ID);
            _deploymentsConnectionStatusLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_STATUS_LABEL_ID);
            _deploymentsConnectionStopBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_SERVER_ACTION_STOP_BTN_ID);

            _footerDocumentationBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.FOOTER_DOCUMENTATION_BTN_ID);
            _footerNeedMoreGameServersBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID);

            _apiEnvironment = EdgegapWindowMetadata.API_ENVIRONMENT; // (!) TODO: Hard-coded while unused in UI
        }

        /// <summary>
        /// Sanity check: If we implicitly changed an #Id, we need to know early so we can update the const.
        /// In order of appearance seen in setVisualElementsToFields().
        /// </summary>
        private void assertVisualElementKeys()
        {
            // MIRROR CHANGE: this doesn't compile in Unity 2019
            /*
            try
            {
                Assert.IsTrue(_apiTokenInput is { name: EdgegapWindowMetadata.API_TOKEN_TXT_ID },
                    $"Expected {nameof(_apiTokenInput)} via #{EdgegapWindowMetadata.API_TOKEN_TXT_ID}");

                Assert.IsTrue(_apiTokenVerifyBtn is { name: EdgegapWindowMetadata.API_TOKEN_VERIFY_BTN_ID },
                    $"Expected {nameof(_apiTokenVerifyBtn)} via #{EdgegapWindowMetadata.API_TOKEN_VERIFY_BTN_ID}");

                Assert.IsTrue(_apiTokenGetBtn is { name: EdgegapWindowMetadata.API_TOKEN_GET_BTN_ID },
                    $"Expected {nameof(_apiTokenGetBtn)} via #{EdgegapWindowMetadata.API_TOKEN_GET_BTN_ID}");

                Assert.IsTrue(_postAuthContainer is { name: EdgegapWindowMetadata.POST_AUTH_CONTAINER_ID },
                    $"Expected {nameof(_postAuthContainer)} via #{EdgegapWindowMetadata.POST_AUTH_CONTAINER_ID}");

                Assert.IsTrue(_appInfoFoldout is { name: EdgegapWindowMetadata.APP_INFO_FOLDOUT_ID },
                    $"Expected {nameof(_appInfoFoldout)} via #{EdgegapWindowMetadata.APP_INFO_FOLDOUT_ID}");

                Assert.IsTrue(_appNameInput is { name: EdgegapWindowMetadata.APP_NAME_TXT_ID },
                    $"Expected {nameof(_appNameInput)} via #{EdgegapWindowMetadata.APP_NAME_TXT_ID}");

                Assert.IsTrue(_appLoadExistingBtn is { name: EdgegapWindowMetadata.APP_LOAD_EXISTING_BTN_ID },
                    $"Expected {nameof(_appLoadExistingBtn)} via #{EdgegapWindowMetadata.APP_LOAD_EXISTING_BTN_ID}");

                Assert.IsTrue(_appIconSpriteObjInput is { name: EdgegapWindowMetadata.APP_ICON_SPRITE_OBJ_ID },
                    $"Expected {nameof(_appIconSpriteObjInput)} via #{EdgegapWindowMetadata.APP_ICON_SPRITE_OBJ_ID}");

                Assert.IsTrue(_appCreateBtn is { name: EdgegapWindowMetadata.APP_CREATE_BTN_ID },
                    $"Expected {nameof(_appCreateBtn)} via #{EdgegapWindowMetadata.APP_CREATE_BTN_ID}");

                Assert.IsTrue(_appCreateResultLabel is { name: EdgegapWindowMetadata.APP_CREATE_RESULT_LABEL_ID },
                    $"Expected {nameof(_appCreateResultLabel)} via #{EdgegapWindowMetadata.APP_CREATE_RESULT_LABEL_ID}");

                Assert.IsTrue(_containerRegistryFoldout is { name: EdgegapWindowMetadata.CONTAINER_REGISTRY_FOLDOUT_ID },
                    $"Expected {nameof(_containerRegistryFoldout)} via #{EdgegapWindowMetadata.CONTAINER_REGISTRY_FOLDOUT_ID}");

                Assert.IsTrue(_containerPortNumInput is { name: EdgegapWindowMetadata.CONTAINER_REGISTRY_PORT_NUM_ID },
                    $"Expected {nameof(_containerPortNumInput)} via #{EdgegapWindowMetadata.CONTAINER_REGISTRY_PORT_NUM_ID}");

                // MIRROR CHANGE: disable and replaced with hardcoded port type dropdown
                // Assert.IsTrue(_containerTransportTypeEnumInput is { name: EdgegapWindowMetadata.CONTAINER_REGISTRY_TRANSPORT_TYPE_ENUM_ID },
                //     $"Expected {nameof(_containerTransportTypeEnumInput)} via #{EdgegapWindowMetadata.CONTAINER_REGISTRY_TRANSPORT_TYPE_ENUM_ID}");
                // END MIRROR CHANGE

                Assert.IsTrue(_containerUseCustomRegistryToggle is { name: EdgegapWindowMetadata.CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID },
                    $"Expected {nameof(_containerUseCustomRegistryToggle)} via #{EdgegapWindowMetadata.CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID}");

                Assert.IsTrue(_containerCustomRegistryWrapper is { name: EdgegapWindowMetadata.CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID },
                    $"Expected {nameof(_containerCustomRegistryWrapper)} via #{EdgegapWindowMetadata.CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID}");

                Assert.IsTrue(_containerRegistryUrlInput is { name: EdgegapWindowMetadata.CONTAINER_REGISTRY_URL_TXT_ID },
                    $"Expected {nameof(_containerRegistryUrlInput)} via #{EdgegapWindowMetadata.CONTAINER_REGISTRY_URL_TXT_ID}");

                Assert.IsTrue(_containerImageRepositoryInput is { name: EdgegapWindowMetadata.CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID },
                    $"Expected {nameof(_containerImageRepositoryInput)} via #{EdgegapWindowMetadata.CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID}");

                Assert.IsTrue(_containerUsernameInput is { name: EdgegapWindowMetadata.CONTAINER_USERNAME_TXT_ID },
                    $"Expected {nameof(_containerUsernameInput)} via #{EdgegapWindowMetadata.CONTAINER_USERNAME_TXT_ID}");

                Assert.IsTrue(_containerTokenInput is { name: EdgegapWindowMetadata.CONTAINER_TOKEN_TXT_ID },
                    $"Expected {nameof(_containerTokenInput)} via #{EdgegapWindowMetadata.CONTAINER_TOKEN_TXT_ID}");


                Assert.IsTrue(_containerTokenInput is { name: EdgegapWindowMetadata.CONTAINER_TOKEN_TXT_ID },
                    $"Expected {nameof(_containerTokenInput)} via #{EdgegapWindowMetadata.CONTAINER_TOKEN_TXT_ID}");

                Assert.IsTrue(_containerBuildAndPushResultLabel is { name: EdgegapWindowMetadata.CONTAINER_BUILD_AND_PUSH_RESULT_LABEL_ID },
                    $"Expected {nameof(_containerBuildAndPushResultLabel)} via #{EdgegapWindowMetadata.CONTAINER_BUILD_AND_PUSH_RESULT_LABEL_ID}");

                Assert.IsTrue(_deploymentsFoldout is { name: EdgegapWindowMetadata.DEPLOYMENTS_FOLDOUT_ID },
                    $"Expected {nameof(_deploymentsFoldout)} via #{EdgegapWindowMetadata.DEPLOYMENTS_FOLDOUT_ID}");

                Assert.IsTrue(_deploymentsRefreshBtn is { name: EdgegapWindowMetadata.DEPLOYMENTS_REFRESH_BTN_ID },
                    $"Expected {nameof(_deploymentsRefreshBtn)} via #{EdgegapWindowMetadata.DEPLOYMENTS_REFRESH_BTN_ID}");

                Assert.IsTrue(_deploymentsCreateBtn is { name: EdgegapWindowMetadata.DEPLOYMENTS_CREATE_BTN_ID },
                    $"Expected {nameof(_deploymentsCreateBtn)} via #{EdgegapWindowMetadata.DEPLOYMENTS_CREATE_BTN_ID}");

                Assert.IsTrue(_deploymentsStatusLabel is { name: EdgegapWindowMetadata.DEPLOYMENTS_STATUS_LABEL_ID },
                    $"Expected {nameof(_deploymentsStatusLabel)} via #{EdgegapWindowMetadata.DEPLOYMENTS_STATUS_LABEL_ID}");

                Assert.IsTrue(_deploymentsServerDataContainer is { name: EdgegapWindowMetadata.DEPLOYMENTS_CONTAINER_ID },
                    $"Expected {nameof(_deploymentsServerDataContainer)} via #{EdgegapWindowMetadata.DEPLOYMENTS_CONTAINER_ID}");

                Assert.IsTrue(_deploymentConnectionCopyUrlBtn is { name: EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_COPY_URL_BTN_ID },
                    $"Expected {nameof(_deploymentConnectionCopyUrlBtn)} via #{EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_COPY_URL_BTN_ID}");

                Assert.IsTrue(_deploymentsConnectionUrlReadonlyInput is { name: EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_URL_READONLY_TXT_ID },
                    $"Expected {nameof(_deploymentsConnectionUrlReadonlyInput)} via #{EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_URL_READONLY_TXT_ID}");

                Assert.IsTrue(_deploymentsConnectionStatusLabel is { name: EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_STATUS_LABEL_ID },
                    $"Expected {nameof(_deploymentsConnectionStatusLabel)} via #{EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_STATUS_LABEL_ID}");

                Assert.IsTrue(_deploymentsConnectionStopBtn is { name: EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_SERVER_ACTION_STOP_BTN_ID },
                    $"Expected {nameof(_deploymentsConnectionStopBtn)} via #{EdgegapWindowMetadata.DEPLOYMENTS_CONNECTION_SERVER_ACTION_STOP_BTN_ID}");


                Assert.IsTrue(_footerDocumentationBtn is { name: EdgegapWindowMetadata.FOOTER_DOCUMENTATION_BTN_ID },
                    $"Expected {nameof(_footerDocumentationBtn)} via #{EdgegapWindowMetadata.FOOTER_DOCUMENTATION_BTN_ID}");

                Assert.IsTrue(_footerNeedMoreGameServersBtn is { name: EdgegapWindowMetadata.FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID },
                    $"Expected {nameof(_footerNeedMoreGameServersBtn)} via #{EdgegapWindowMetadata.FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID}");


                // // TODO: Explicitly set, for now in v2 - but remember to assert later if we stop hard-coding these >>
                // _apiEnvironment
                // _appVersionName
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                _postAuthContainer.SetEnabled(false);
            }
            */ // END MIRROR CHANGE
        }

        /// <summary>
        /// Register non-btn change actionss. We'll want to save for persistence, validate, etc
        /// </summary>
        private void registerFieldCallbacks()
        {
            _apiTokenInput.RegisterValueChangedCallback(onApiTokenInputChanged);
            _apiTokenInput.RegisterCallback<FocusOutEvent>(onApiTokenInputFocusOut);

            _appNameInput.RegisterValueChangedCallback(onAppNameInputChanged);
            _containerPortNumInput.RegisterCallback<FocusOutEvent>(onContainerPortNumInputFocusOut);

            _containerUseCustomRegistryToggle.RegisterValueChangedCallback(onContainerUseCustomRegistryToggle);
            _containerNewTagVersionInput.RegisterValueChangedCallback(onContainerNewTagVersionInputChanged);
        }

        /// <summary>
        /// Prevents memory leaks, mysterious errors and "ghost" values set from a previous session.
        /// Should parity the opposute of registerFieldCallbacks().
        /// </summary>
        private void unregisterFieldCallbacks()
        {
            _apiTokenInput.UnregisterValueChangedCallback(onApiTokenInputChanged);
            _apiTokenInput.UnregisterCallback<FocusOutEvent>(onApiTokenInputFocusOut);

            _containerUseCustomRegistryToggle.UnregisterValueChangedCallback(onContainerUseCustomRegistryToggle);
            _containerPortNumInput.UnregisterCallback<FocusOutEvent>(onContainerPortNumInputFocusOut);
        }

        /// <summary>
        /// Register click actions, mostly from buttons: Need to -= unregistry them @ OnDisable()
        /// </summary>
        private void registerClickCallbacks()
        {
            _debugBtn.clickable.clicked += onDebugBtnClick;

            _apiTokenVerifyBtn.clickable.clicked += onApiTokenVerifyBtnClick;
            _apiTokenGetBtn.clickable.clicked += onApiTokenGetBtnClick;

            _appCreateBtn.clickable.clicked += onAppCreateBtnClickAsync;
            _appLoadExistingBtn.clickable.clicked += onAppLoadExistingBtnClickAsync;

            _containerBuildAndPushServerBtn.clickable.clicked += onContainerBuildAndPushServerBtnClickAsync;
            _deploymentConnectionCopyUrlBtn.clickable.clicked += onDeploymentConnectionCopyUrlBtnClick;

            _deploymentsRefreshBtn.clickable.clicked += onDeploymentsRefreshBtnClick;
            _deploymentsCreateBtn.clickable.clicked += onDeploymentCreateBtnClick;

            _footerDocumentationBtn.clickable.clicked += onFooterDocumentationBtnClick;
            _footerNeedMoreGameServersBtn.clickable.clicked += onFooterNeedMoreGameServersBtnClick;
        }

        /// <summary>
        /// Prevents memory leaks, mysterious errors and "ghost" values set from a previous session.
        /// Should parity the opposute of registerClickEvents().
        /// </summary>
        private void unregisterClickEvents()
        {
            _debugBtn.clickable.clicked -= onDebugBtnClick;

            _apiTokenVerifyBtn.clickable.clicked -= onApiTokenVerifyBtnClick;
            _apiTokenGetBtn.clickable.clicked -= onApiTokenGetBtnClick;

            _appCreateBtn.clickable.clicked -= onAppCreateBtnClickAsync;
            _appLoadExistingBtn.clickable.clicked -= onAppLoadExistingBtnClickAsync;

            _containerBuildAndPushServerBtn.clickable.clicked -= onContainerBuildAndPushServerBtnClickAsync;
            _deploymentConnectionCopyUrlBtn.clickable.clicked -= onDeploymentConnectionCopyUrlBtnClick;

            _deploymentsRefreshBtn.clickable.clicked -= onDeploymentsRefreshBtnClick;
            _deploymentsCreateBtn.clickable.clicked -= onDeploymentCreateBtnClick;

            _footerDocumentationBtn.clickable.clicked -= onFooterDocumentationBtnClick;
            _footerNeedMoreGameServersBtn.clickable.clicked -= onFooterNeedMoreGameServersBtnClick;
        }

        private void initToggleDynamicUi()
        {
            hideResultLabels();
            _deploymentsRefreshBtn.SetEnabled(false);
            loadPersistentDataFromEditorPrefs();
            setDeploymentBtnsFromCache();
            _debugBtn.visible = EdgegapWindowMetadata.SHOW_DEBUG_BTN;
        }

        /// <summary>
        /// Based on existing _deploymentsConnection[Url|Status]Label txt
        /// </summary>
        private void setDeploymentBtnsFromCache()
        {
            bool showDeploymentConnectionStopBtn = !string.IsNullOrEmpty(_deploymentsConnectionUrlReadonlyInput.text);
            if (!showDeploymentConnectionStopBtn)
                return;

            // We found some leftover connection cache >>
            _deploymentsConnectionStopBtn.visible = true;
            _deploymentsRefreshBtn.SetEnabled(true);

            // Enable stop btn?
            bool isDeployed = _deploymentsConnectionStatusLabel.text.ToLowerInvariant().Contains("deployed");
            _deploymentsConnectionStopBtn.SetEnabled(isDeployed);
            if (isDeployed)
                _deploymentsConnectionStopBtn.clickable.clickedWithEventInfo += onDynamicStopServerBtnAsync; // Unsub'd from within
        }

        /// <summary>For example, result labels (success/err) should be hidden on init</summary>
        private void hideResultLabels()
        {
            _appCreateResultLabel.visible = false;
            _containerBuildAndPushResultLabel.visible = false;
            _deploymentsStatusLabel.style.display = DisplayStyle.None;
        }


        #region Init -> Button clicks
        /// <summary>
        /// Experiment here! You may want to log what you're doing
        /// in case you inadvertently leave it on.
        /// </summary>
        private void onDebugBtnClick() => debugEnableAllGroups();

        private void debugEnableAllGroups()
        {
            Debug.Log("debugEnableAllGroups");

            _appInfoFoldout.SetEnabled(true);
            _appInfoFoldout.SetEnabled(true);
            _containerRegistryFoldout.SetEnabled(true);
            _deploymentsFoldout.SetEnabled(true);

            if (_containerUseCustomRegistryToggle.value)
                _containerCustomRegistryWrapper.SetEnabled(true);
        }

        private void onApiTokenVerifyBtnClick() => _ = verifyApiTokenGetRegistryCredsAsync();
        private void onApiTokenGetBtnClick() => openGetApiTokenWebsite();

        /// <summary>Process UI + validation before/after API logic</summary>
        private async void onAppCreateBtnClickAsync()
        {
            // Assert data locally before calling API
            assertAppNameExists();

            _appCreateResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor("Creating...",
                EdgegapWindowMetadata.StatusColors.Processing);

            try { await createAppAsync(); }
            finally
            {
                _appCreateBtn.SetEnabled(checkHasAppName());
                _appCreateResultLabel.visible = _appCreateResultLabel.text != EdgegapWindowMetadata.LOADING_RICH_STR;
            }
        }

        /// <summary>Process UI + validation before/after API logic</summary>
        private async void onAppLoadExistingBtnClickAsync()
        {
            // Assert UI data locally before calling API
            assertAppNameExists();

            try { await GetAppAsync(); }
            finally
            {
                _appLoadExistingBtn.SetEnabled(checkHasAppName());
                _appCreateResultLabel.visible = _appCreateResultLabel.text != EdgegapWindowMetadata.LOADING_RICH_STR;
            }
        }

        /// <summary>Copy url to clipboard</summary>
        private void onDeploymentConnectionCopyUrlBtnClick()
        {
            if (string.IsNullOrEmpty(_deploymentsConnectionUrlReadonlyInput.value))
                return; // Nothing to copy

            EditorGUIUtility.systemCopyBuffer = _deploymentsConnectionUrlReadonlyInput.value;
            _deploymentsStatusLabel.text = EdgegapWindowMetadata.WrapRichTextInColor("Copied URL!",
                EdgegapWindowMetadata.StatusColors.Success);
            _deploymentsStatusLabel.style.display = DisplayStyle.Flex;
            _ = clearDeploymentStatusAfterDelay(seconds: 1);
        }

        private async Task clearDeploymentStatusAfterDelay(int seconds)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            _deploymentsStatusLabel.style.display = DisplayStyle.None;
        }

        /// <summary>Process UI + validation before/after API logic</summary>
        private async void onContainerBuildAndPushServerBtnClickAsync()
        {
            // Assert data locally before calling API
            // Validate custom container registry, app name
            try
            {
                assertAppNameExists();
                Assert.IsTrue(
                    !_containerImageRepositoryInput.value.EndsWith("/"),
                    $"Expected {nameof(_containerImageRepositoryInput)} to !contain " +
                    "trailing slash (should end with /appName)");
            }
            catch (Exception e)
            {
                Debug.LogError($"onContainerBuildAndPushServerBtnClickAsync Error: {e}");
                throw;
            }

            // Hide previous result labels, disable btns (to reenable when done)
            hideResultLabels();
            _containerBuildAndPushServerBtn.SetEnabled(false);

            // Show new loading status
            _containerBuildAndPushResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                EdgegapWindowMetadata.PROCESSING_RICH_STR,
                EdgegapWindowMetadata.StatusColors.Processing);

            try { await buildAndPushServerAsync(); }
            finally
            {
                _containerBuildAndPushServerBtn.SetEnabled(checkHasAppName());
                _containerBuildAndPushResultLabel.visible = _containerBuildAndPushResultLabel.text != EdgegapWindowMetadata.PROCESSING_RICH_STR;
            }
        }

        private bool checkHasAppName() => _appNameInput.value.Length > 0;
        private void onDeploymentsRefreshBtnClick() => _ = refreshDeploymentsAsync();
        private void onFooterDocumentationBtnClick() => openDocumentationWebsite();
        private void onFooterNeedMoreGameServersBtnClick() => openNeedMoreGameServersWebsite();

        /// <summary>AKA "Create New Deployment" Btn</summary>
        private void onDeploymentCreateBtnClick() => _ = createDeploymentStartServerAsync();
        #endregion // Init -> /Button Clicks
        #endregion // Init


        /// <summary>Throw if !appName val</summary>
        private void assertAppNameExists() =>
            Assert.IsTrue(!string.IsNullOrEmpty(_appNameInput.value),
                $"Expected {nameof(_appNameInput)} val");

        /// <summary>
        /// Save persistent read-only data: If the human didn't type it, it won't save automatically.
        /// </summary>
        private void SyncObjectWithForm()
        {
            _appIconSpriteObj = _appIconSpriteObjInput.value as Sprite;
        }

        /// <summary>TODO: Load persistent data?</summary>
        private void syncFormWithObjectStatic()
        {
            // Only show the rest of the form if apiToken is verified
            _postAuthContainer.SetEnabled(_isApiTokenVerified);
            _appIconSpriteObjInput.value = _appIconSpriteObj;
            _containerCustomRegistryWrapper.SetEnabled(_containerUseCustomRegistryToggle.value);
            _containerUseCustomRegistryToggle.value = _containerUseCustomRegistryToggle.value;

            // Only enable certain elements if appName exists
            bool hasAppName = checkHasAppName();
            _appCreateBtn.SetEnabled(hasAppName);
            _appLoadExistingBtn.SetEnabled(hasAppName);
        }

        /// <summary>
        /// Dynamically set form based on API call results.
        /// => If APIToken is cached via EditorPrefs, verify => gets registry creds.
        /// => If appName is cached via ViewDataKey, loads the app.
        /// </summary>
        private async Task syncFormWithObjectDynamicAsync()
        {
            if (string.IsNullOrEmpty(_apiTokenInput.value))
                return;

            // We found a cached api token: Verify =>
            if (IsLogLevelDebug) Debug.Log("syncFormWithObjectDynamicAsync: Found apiToken; " +
                "calling verifyApiTokenGetRegistryCredsAsync =>");
            await verifyApiTokenGetRegistryCredsAsync();

            // Was the API token verified + we found a cached app name? Load the app =>
            // But ignore errs, since we're just *assuming* the app exists since the appName was filled
            if (_isApiTokenVerified && checkHasAppName())
            {
                if (IsLogLevelDebug) Debug.Log("syncFormWithObjectDynamicAsync: Found apiToken && appName; " +
                    "calling GetAppAsync =>");
                try { await GetAppAsync(); }
                finally { _appLoadExistingBtn.SetEnabled(checkHasAppName()); }
            }
        }


        #region Immediate non-button changes
        /// <summary>
        /// On change, validate -> update custom container registry suffix.
        /// Toggle create app btn if 1+ char
        /// </summary>
        /// <param name="evt"></param>
        private void onAppNameInputChanged(ChangeEvent<string> evt)
        {
            // Validate: Only allow alphanumeric, underscore, dash, plus, period
            if (!_appNameAllowedCharsRegex.IsMatch(evt.newValue))
                _appNameInput.value = evt.previousValue; // Revert to the previous value
            else
                setContainerImageRepositoryVal(); // Valid -> Update the custom container registry suffix

            // Toggle btns on 1+ char entered
            bool hasAppName = checkHasAppName();
            _appCreateBtn.SetEnabled(hasAppName);
            _appLoadExistingBtn.SetEnabled(hasAppName);
        }

        /// <summary>On focus out, clamp port between 1024~49151</summary>
        /// <param name="evt"></param>
        private void onContainerPortNumInputFocusOut(FocusOutEvent evt)
        {
            // Use TryParse to avoid exceptions
            if (int.TryParse(_containerPortNumInput.value, out int port))
            {
                // Clamp the port to the range and set the value back to the TextField
                _containerPortNumInput.value = Mathf.Clamp(
                    port,
                    EdgegapWindowMetadata.PORT_MIN,
                    EdgegapWindowMetadata.PORT_MAX)
                    .ToString();
            }
            else
            {
                // If input is !valid, set to default
                _containerPortNumInput.value = EdgegapWindowMetadata.PORT_DEFAULT.ToString();
            }
        }

        /// <summary>
        /// While changing the token, we temporarily unmask. On change, set state to !verified.
        /// </summary>
        /// <param name="evt"></param>
        private void onApiTokenInputChanged(ChangeEvent<string> evt)
        {
            // Unmask while changing
            TextField apiTokenTxt = evt.target as TextField;
            apiTokenTxt.isPasswordField = false;

            // Token changed? Reset form to !verified state and fold all groups
            _isApiTokenVerified = false;
            _postAuthContainer.SetEnabled(false);
            closeDisableGroups();

            // Toggle "Verify" btn on 1+ char entered
            _apiTokenVerifyBtn.SetEnabled(evt.newValue.Length > 0);
        }

        /// <summary>Unmask while typing</summary>
        /// <param name="evt"></param>
        private void onApiTokenInputFocusOut(FocusOutEvent evt)
        {
            TextField apiTokenTxt = evt.target as TextField;
            apiTokenTxt.isPasswordField = true;
        }

        /// <summary>On toggle, enable || disable the custom registry inputs (below the Toggle).</summary>
        private void onContainerUseCustomRegistryToggle(ChangeEvent<bool> evt) =>
            _containerCustomRegistryWrapper.SetEnabled(evt.newValue);

        /// <summary>On empty, we fallback to "latest", a fallback val from EdgegapWindowMetadata.cs</summary>
        /// <param name="evt"></param>
        private void onContainerNewTagVersionInputChanged(ChangeEvent<string> evt)
        {
            if (!string.IsNullOrEmpty(evt.newValue))
                return;

            // Set fallback value -> select all for UX, since the user may not expect this
            _containerNewTagVersionInput.value = EdgegapWindowMetadata.DEFAULT_VERSION_TAG;
            _containerNewTagVersionInput.SelectAll();
        }
        #endregion // Immediate non-button changes


        /// <summary>
        /// Used for converting a Sprite to a base64 string: By default, textures are !readable,
        /// and we don't want to have to instruct users how to make it readable for UX.
        /// Instead, we'll make a copy of that texture -> make it readable.
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        private Texture2D makeTextureReadable(Texture2D original)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                original.width,
                original.height
            );

            Graphics.Blit(original, rt);
            Texture2D readableTexture = new Texture2D(original.width, original.height);

            Rect rect = new Rect(
                0,
                0,
                rt.width,
                rt.height);

            readableTexture.ReadPixels(rect, destX: 0, destY: 0);
            readableTexture.Apply();
            RenderTexture.ReleaseTemporary(rt);

            return readableTexture;
        }

        /// <summary>From Base64 string -> to Sprite</summary>
        /// <param name="imgBase64Str">Edgegap build app requires a max size of 200</param>
        /// <returns>Sprite</returns>
        private Sprite getSpriteFromBase64Str(string imgBase64Str)
        {
            if (string.IsNullOrEmpty(imgBase64Str))
                return null;

            try
            {
                byte[] imageBytes = Convert.FromBase64String(imgBase64Str);

                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(imageBytes);

                Rect rect = new Rect( // MIRROR CHANGE: 'new()' not supported in Unity 2020
                    x: 0.0f,
                    y: 0.0f,
                    texture.width,
                    texture.height);

                return Sprite.Create(
                    texture,
                    rect,
                    pivot: new Vector2(0.5f, 0.5f),
                    pixelsPerUnit: 100.0f);
            }
            catch (Exception e)
            {
                Debug.Log($"Warning: getSpriteFromBase64Str failed (returning null) - {e}");
                return null;
            }
        }

        /// <summary>From Sprite -> to Base64 string</summary>
        /// <param name="sprite"></param>
        /// <param name="maxKbSize">Edgegap build app requires a max size of 200</param>
        /// <returns>imageBase64Str</returns>
        private string getBase64StrFromSprite(Sprite sprite, int maxKbSize = 200)
        {
            if (sprite == null)
                return null;

            try
            {
                Texture2D texture = makeTextureReadable(sprite.texture);

                // Crop the texture to the sprite's rectangle (instead of the entire texture)
                Texture2D croppedTexture = new Texture2D(
                    (int)sprite.rect.width,
                    (int)sprite.rect.height);

                Color[] pixels = texture.GetPixels(
                    (int)sprite.rect.x,
                    (int)sprite.rect.y,
                    (int)sprite.rect.width,
                    (int)sprite.rect.height
                );

                croppedTexture.SetPixels(pixels);
                croppedTexture.Apply();

                // Encode to PNG ->
                byte[] textureBytes = croppedTexture.EncodeToPNG();

                // Validate size
                const int oneKb = 1024;
                int pngTextureSizeKb = textureBytes.Length / oneKb;
                bool isPngLessThanMaxSize = pngTextureSizeKb < maxKbSize;

                if (!isPngLessThanMaxSize)
                {
                    textureBytes = croppedTexture.EncodeToJPG();
                    int jpgTextureSizeKb = textureBytes.Length / oneKb;
                    bool isJpgLessThanMaxSize = pngTextureSizeKb < maxKbSize;

                    Assert.IsTrue(isJpgLessThanMaxSize, $"Expected texture PNG to be < {maxKbSize}kb " +
                        $"in size (but found {jpgTextureSizeKb}kb); then tried JPG, but is still {jpgTextureSizeKb}kb in size");
                    Debug.LogWarning($"App icon PNG was too large (max {maxKbSize}), so we converted to JPG");
                }

                string base64ImageString = Convert.ToBase64String(textureBytes); // eg: "Aaabbcc=="
                return base64ImageString;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verifies token => apps/container groups -> gets registry creds (if any).
        /// TODO: UX - Show loading spinner.
        /// </summary>
        private async Task verifyApiTokenGetRegistryCredsAsync()
        {
            if (IsLogLevelDebug) Debug.Log("verifyApiTokenGetRegistryCredsAsync");

            // Disable most ui while we verify
            _isApiTokenVerified = false;
            _apiTokenVerifyBtn.SetEnabled(false);
            SyncContainerEnablesToState();
            hideResultLabels();

            EdgegapWizardApi wizardApi = getWizardApi();
            EdgegapHttpResult initQuickStartResultCode = await wizardApi.InitQuickStart();

            _apiTokenVerifyBtn.SetEnabled(true);
            _isApiTokenVerified = initQuickStartResultCode.IsResultCode204;

            if (!_isApiTokenVerified)
            {
                SyncContainerEnablesToState();
                return;
            }

            // Verified: Let's see if we have active registry credentials // TODO: This will later be a result model
            EdgegapHttpResult<GetRegistryCredentialsResult> getRegistryCredentialsResult = await wizardApi.GetRegistryCredentials();

            if (getRegistryCredentialsResult.IsResultCode200)
            {
                // Success
                _credentials = getRegistryCredentialsResult.Data;
                persistUnmaskedApiToken(_apiTokenInput.value);
                prefillContainerRegistryForm(_credentials);
            }
            else
            {
                // Fail
            }

            // Unlock the rest of the form, whether we prefill the container registry or not
            SyncContainerEnablesToState();
        }

        /// <summary>
        /// We have container registry params; we'll prefill registry container fields.
        /// </summary>
        /// <param name="credentials">GetRegistryCredentialsResult</param>
        private void prefillContainerRegistryForm(GetRegistryCredentialsResult credentials)
        {
            if (IsLogLevelDebug) Debug.Log("prefillContainerRegistryForm");

            if (credentials == null)
                throw new Exception($"!{nameof(credentials)}");

            _containerRegistryUrlInput.value = credentials.RegistryUrl;

            setContainerImageRepositoryVal();
            _containerUsernameInput.value = credentials.Username;
            _containerTokenInput.value = credentials.Token;
        }

        /// <summary>
        /// Sets to "{credentials.Project}/{appName}" from cached credentials, forcing lowercased appName.
        /// </summary>
        private void setContainerImageRepositoryVal()
        {
            // ex: "xblade1-9sa8dfh9sda8hf/mygame1"
            string project = _credentials?.Project ?? "";
            string appName = _appNameInput?.value.ToLowerInvariant() ?? "";
            _containerImageRepositoryInput.value = $"{project}/{appName}";
        }

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

        /// <summary>
        /// Toggle container groups and foldouts on/off based on:
        /// - _isApiTokenVerified
        /// </summary>
        private void SyncContainerEnablesToState()
        {
            // Requires _isApiTokenVerified
            _postAuthContainer.SetEnabled(_isApiTokenVerified); // Entire body container
            _appInfoFoldout.SetEnabled(_isApiTokenVerified);
            _appInfoFoldout.value = _isApiTokenVerified;

            // + Requires _isContainerRegistryReady
            bool isApiTokenVerifiedAndContainerReady = _isApiTokenVerified && _isContainerRegistryReady;

            _containerRegistryFoldout.SetEnabled(isApiTokenVerifiedAndContainerReady);
            _containerRegistryFoldout.value = isApiTokenVerifiedAndContainerReady;

            _deploymentsFoldout.SetEnabled(isApiTokenVerifiedAndContainerReady);
            _deploymentsFoldout.value = isApiTokenVerifiedAndContainerReady;

            // + Requires _containerUseCustomRegistryToggleBool
            _containerCustomRegistryWrapper.SetEnabled(isApiTokenVerifiedAndContainerReady &&
                _containerUseCustomRegistryToggle.value);
        }

        private void openGetApiTokenWebsite()
        {
            if (IsLogLevelDebug) Debug.Log("openGetApiTokenWebsite");
            Application.OpenURL(EdgegapWindowMetadata.EDGEGAP_GET_A_TOKEN_URL);
        }

        /// <returns>isSuccess; sets _isContainerRegistryReady + _loadedApp</returns>
        private async Task<bool> GetAppAsync()
        {
            if (IsLogLevelDebug) Debug.Log("GetAppAsync");

            // Hide previous result labels, disable btns (to reenable when done)
            hideResultLabels();
            _appCreateBtn.SetEnabled(false);
            _apiTokenVerifyBtn.SetEnabled(false);

            // Show new loading status
            _appCreateResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                EdgegapWindowMetadata.LOADING_RICH_STR,
                EdgegapWindowMetadata.StatusColors.Processing);
            _appCreateResultLabel.visible = true;

            EdgegapAppApi appApi = getAppApi();
            EdgegapHttpResult<GetCreateAppResult> getAppResult = await appApi.GetApp(_appNameInput.value);
            onGetCreateApplicationResult(getAppResult);

            return _isContainerRegistryReady;
        }

        /// <summary>
        /// TODO: Add err handling for reaching app limit (max 2 for free tier).
        /// </summary>
        private async Task createAppAsync()
        {
            if (IsLogLevelDebug) Debug.Log("createAppAsync");

            // Hide previous result labels, disable btns (to reenable when done)
            hideResultLabels();
            _appCreateBtn.SetEnabled(false);
            _apiTokenVerifyBtn.SetEnabled(false);

            EdgegapAppApi appApi = getAppApi();
            CreateAppRequest createAppRequest = new CreateAppRequest( // MIRROR CHANGE: 'new()' not supported in Unity 2020
                _appNameInput.value,
                isActive: true,
                getBase64StrFromSprite(_appIconSpriteObj) ?? "");

            EdgegapHttpResult<GetCreateAppResult> createAppResult = await appApi.CreateApp(createAppRequest);
            onGetCreateApplicationResult(createAppResult);
        }

        /// <summary>Get || Create results both handled here. On success, sets _isContainerRegistryReady + _loadedApp data</summary>
        /// <param name="result"></param>
        private void onGetCreateApplicationResult(EdgegapHttpResult<GetCreateAppResult> result)
        {
            // Assert the result itself || result's create time exists
            bool isSuccess = result.IsResultCode200 || result.IsResultCode409; // 409 == app already exists
            _isContainerRegistryReady = isSuccess;
            _loadedApp = result.Data;

            _appCreateResultLabel.text = getFriendlyCreateAppResultStr(result);
            _containerRegistryFoldout.value = _isContainerRegistryReady;
            _appCreateBtn.SetEnabled(true);
            _apiTokenVerifyBtn.SetEnabled(true);
            SyncContainerEnablesToState();

            // Only show status label if we're init'd; otherwise, we auto-tried to get the existing app that
            // we knew had a chance of not being there
            _appCreateResultLabel.visible = IsInitd;

            // App base64 img? Parse to sprite, overwrite app image UI/cache
            if (!string.IsNullOrEmpty(_loadedApp.Image))
            {
                _appIconSpriteObj = getSpriteFromBase64Str(_loadedApp.Image);
                _appIconSpriteObjInput.value = _appIconSpriteObj;
            }

            // On fail, shake the "Add more game servers" btn // 400 == # of apps limit reached
            bool isCreate = result.HttpMethod == HttpMethod.Post;
            bool isCreateFailAppNumCapMaxed = isCreate && !_isContainerRegistryReady && result.IsResultCode400;
            if (isCreateFailAppNumCapMaxed)
                shakeNeedMoreGameServersBtn();
        }

        /// <summary>Slight animation shake</summary>
        private void shakeNeedMoreGameServersBtn()
        {
            ButtonShaker shaker = new ButtonShaker(_footerNeedMoreGameServersBtn);
            _ = shaker.ApplyShakeAsync();
        }

        /// <returns>Generally "Success" || "Error: {error}" || "Warning: {error}"</returns>
        private string getFriendlyCreateAppResultStr(EdgegapHttpResult<GetCreateAppResult> createAppResult)
        {
            string coloredResultStr = null;

            if (!_isContainerRegistryReady)
            {
                // Error
                string resultStr = $"<b>Error:</b> {createAppResult?.Error?.ErrorMessage}";
                coloredResultStr = EdgegapWindowMetadata.WrapRichTextInColor(
                    resultStr, EdgegapWindowMetadata.StatusColors.Error);
            }
            else if (createAppResult.IsResultCode409)
            {
                // Warn: App already exists - Still success, but just a warn
                string resultStr = $"<b>Warning:</b> {createAppResult.Error.ErrorMessage}";
                coloredResultStr = EdgegapWindowMetadata.WrapRichTextInColor(
                    resultStr, EdgegapWindowMetadata.StatusColors.Warn);
            }
            else
            {
                // Success
                coloredResultStr = EdgegapWindowMetadata.WrapRichTextInColor(
                    "Success", EdgegapWindowMetadata.StatusColors.Success);
            }

            return coloredResultStr;
        }

        /// <summary>Open contact form in desired locale</summary>
        private void openNeedMoreGameServersWebsite() =>
            Application.OpenURL(EdgegapWindowMetadata.EDGEGAP_ADD_MORE_GAME_SERVERS_URL);

        private void openDocumentationWebsite()
        {
            // MIRROR CHANGE
            /*
            string documentationUrl = _apiEnvironment.GetDocumentationUrl();

            if (!string.IsNullOrEmpty(documentationUrl))
                Application.OpenURL(documentationUrl);
            else
            {
                string apiEnvName = Enum.GetName(typeof(ApiEnvironment), _apiEnvironment);
                Debug.LogWarning($"Could not open documentation for api environment " +
                    $"{apiEnvName}: No documentation URL.");
            }
            */

            // link to our step by step guide
            Application.OpenURL("https://mirror-networking.gitbook.io/docs/hosting/edgegap-hosting-plugin-guide");
            // END MIRROR CHANGE
        }

        /// <summary>
        /// Currently only refreshes an existing deployment. AKA "OnRefresh".
        /// TODO: Consider dynamically adding the entire list via GET all deployments.
        /// </summary>
        private async Task refreshDeploymentsAsync()
        {
            if (IsLogLevelDebug) Debug.Log("refreshDeploymentsAsync");

            // Sanity check requestId - if refreshBtn is enabled, we *should* have it
            if (string.IsNullOrEmpty(_deploymentRequestId))
            {
                // We must have stale data - reset
                clearDeploymentConnections();
                return;
            }

            hideResultLabels();
            // clearDeploymentConnections(); // We want to leave the old URL while we only have one
            _deploymentsConnectionStatusLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                "<i>Refreshing...</i>", EdgegapWindowMetadata.StatusColors.Processing);

            EdgegapDeploymentsApi deployApi = getDeployApi();
            EdgegapHttpResult<GetDeploymentStatusResult> getDeploymentStatusResponse =
                await deployApi.GetDeploymentStatusAsync(_deploymentRequestId);

            bool isActiveStatus = getDeploymentStatusResponse?.StatusCode != null &&
                getDeploymentStatusResponse.Data.CurrentStatus == EdgegapWindowMetadata.READY_STATUS;

            if (isActiveStatus)
                onCreateDeploymentOrRefreshSuccess(getDeploymentStatusResponse.Data);
            else
            {
                onCreateDeploymentStartServerFail();
                if (!getDeploymentStatusResponse.HasErr)
                    onRefreshDeploymentStoppedStatus(); // Only a "soft" fail - not a true err
            }
        }

        /// <summary>The deployment simply stopped (a "soft" fail - not an actual err)</summary>
        private void onRefreshDeploymentStoppedStatus()
        {
            // Override to Create fail -- instead, we've simplfy stopped (a "soft" fail)
            _deploymentsConnectionStatusLabel.text = getConnectionStoppedRichStr();
            _deploymentsStatusLabel.style.display = DisplayStyle.None;
            _deploymentsConnectionStopBtn.SetEnabled(false);
            _deploymentsConnectionStopBtn.visible = true;
        }

        /// <summary>Don't use this if you want to keep the last-known connection info.</summary>
        private void clearDeploymentConnections()
        {
            _deploymentsConnectionUrlReadonlyInput.value = "";
            _deploymentsConnectionStatusLabel.text = "";
            _deploymentsConnectionStopBtn.visible = false;
            _deploymentsRefreshBtn.SetEnabled(false);
        }

        /// <summary>
        /// V2 Successor to legacy startServerCallbackAsync() from "Create New Deployment" Btn.
        /// </summary>
        private async Task createDeploymentStartServerAsync()
        {
            // Hide previous result labels, disable btns (to reenable when done)
            if (IsLogLevelDebug) Debug.Log("createDeploymentStartServerAsync");
            hideResultLabels();
            _deploymentsCreateBtn.SetEnabled(false);
            _deploymentsRefreshBtn.SetEnabled(false);
            // _deploymentsConnectionUrlReadonlyInput.value = ""; // We currently want to keep the last known connection, even on err
            _deploymentsConnectionStatusLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                EdgegapWindowMetadata.DEPLOY_REQUEST_RICH_STR,
                EdgegapWindowMetadata.StatusColors.Processing);

            try
            {
                EdgegapDeploymentsApi deployApi = getDeployApi();

                // Get (+cache) external IP async, required to create a deployment. Prioritize cache.
                _userExternalIp = await getExternalIpAddress();

                CreateDeploymentRequest createDeploymentReq = new CreateDeploymentRequest( // MIRROR CHANGE: 'new()' not supported in Unity 2020
                    _appNameInput.value,
                    _containerNewTagVersionInput.value,
                    _userExternalIp);

                // Request to deploy (it won't be active, yet) =>
                EdgegapHttpResult<CreateDeploymentResult> createDeploymentResponse =
                    await deployApi.CreateDeploymentAsync(createDeploymentReq);

                if (!createDeploymentResponse.IsResultCode200)
                {
                    onCreateDeploymentStartServerFail(createDeploymentResponse);
                    return;
                }
                else
                {
                    // Update status
                    _deploymentsConnectionStatusLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                        "<i>Deploying...</i>", EdgegapWindowMetadata.StatusColors.Processing);
                }

                // Check the status of the deployment for READY every 2s =>
                const int pollIntervalSecs = EdgegapWindowMetadata.DEPLOYMENT_READY_STATUS_POLL_SECONDS;
                EdgegapHttpResult<GetDeploymentStatusResult> getDeploymentStatusResponse = await deployApi.AwaitReadyStatusAsync(
                    createDeploymentResponse.Data.RequestId,
                    TimeSpan.FromSeconds(pollIntervalSecs));

                // Process create deployment response
                bool isSuccess = createDeploymentResponse.IsResultCode200;
                if (isSuccess)
                    onCreateDeploymentOrRefreshSuccess(getDeploymentStatusResponse.Data);
                else
                    onCreateDeploymentStartServerFail(createDeploymentResponse);

                _deploymentsStatusLabel.style.display = DisplayStyle.Flex;
            }
            finally
            {
                _deploymentsCreateBtn.SetEnabled(true);
            }
        }

        /// <summary>
        /// CreateDeployment || RefreshDeployment success handler.
        /// </summary>
        /// <param name="getDeploymentStatusResult">Only pass from CreateDeployment</param>
        private void onCreateDeploymentOrRefreshSuccess(GetDeploymentStatusResult getDeploymentStatusResult)
        {
            // Success
            hideResultLabels();
            _deploymentsStatusLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                "Success", EdgegapWindowMetadata.StatusColors.Success);
            _deploymentsStatusLabel.style.display = DisplayStyle.Flex;

            // Cache the deployment result -> persist the requestId
            _lastKnownDeployment = getDeploymentStatusResult;
            _deploymentRequestId = getDeploymentStatusResult.RequestId;
            EditorPrefs.SetString(EdgegapWindowMetadata.DEPLOYMENT_REQUEST_ID_KEY_STR, _deploymentRequestId);

            // ------------
            // Set the static connection row label data >>
            // TODO: This will be dynamically inserted via MVC-style template when we support multiple deployments >>

            // Get external port
            // BUG(WORKAROUND): Expected `ports` to be List<AppPortsData>, but received Dictionary<string, AppPortsData>
            KeyValuePair<string, DeploymentPortsData> portsDataKvp = getDeploymentStatusResult.PortsDict.FirstOrDefault();
            Assert.IsNotNull(portsDataKvp.Value, $"Expected ({nameof(portsDataKvp)} from `getDeploymentStatusResult.PortsDict`)");
            DeploymentPortsData deploymentPortData = portsDataKvp.Value;
            string externalPortStr = deploymentPortData.External.ToString();
            string domainWithExternalPort = $"{getDeploymentStatusResult.Fqdn}:{externalPortStr}";

            _deploymentsConnectionUrlReadonlyInput.value = domainWithExternalPort;
            string newConnectionStatus = EdgegapWindowMetadata.WrapRichTextInColor(
                "Deployed", EdgegapWindowMetadata.StatusColors.Success);

            // Change + Persist read-only fields (ViewDataKeys only save automatically from human input)
            setPersistDeploymentsConnectionUrlLabelTxt(domainWithExternalPort);
            setPersistDeploymentsConnectionStatusLabelTxt(newConnectionStatus);

            // ------------
            // Configure + show stop button
            _deploymentsConnectionStopBtn.clickable.clickedWithEventInfo += onDynamicStopServerBtnAsync; // Unsubscribes on click
            _deploymentsConnectionStopBtn.visible = true;
            _deploymentsConnectionStopBtn.SetEnabled(true);

            // Show refresh btn (currently targeting only this one)
            _deploymentsRefreshBtn.SetEnabled(true);
        }

        private void onCreateDeploymentStartServerFail(EdgegapHttpResult<CreateDeploymentResult> result = null)
        {
            _deploymentsConnectionStatusLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                "Failed to Start", EdgegapWindowMetadata.StatusColors.Error);

            _deploymentsStatusLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                result?.Error.ErrorMessage ?? "Unknown Error",
                EdgegapWindowMetadata.StatusColors.Error);
            _deploymentsStatusLabel.style.display = DisplayStyle.Flex;
            _deploymentsRefreshBtn.SetEnabled(true);

            Debug.Log("(!) Check your deployments here: https://app.edgegap.com/deployment-management/deployments/list");

            // Shake "need more servers" btn on 403
            // MIRROR CHANGE: use old C# syntax that is supported in Unity 2019
            // bool reachedNumDeploymentsHardcap = result is { IsResultCode403: true };
            bool reachedNumDeploymentsHardcap = result != null && result.IsResultCode403;
            // END MIRROR CHANGE
            if (reachedNumDeploymentsHardcap)
                shakeNeedMoreGameServersBtn();
        }

        /// <summary>
        /// This is triggered from a dynamic button, so we need to pass in the event info (TODO: Use evt info later).
        /// </summary>
        /// <param name="evt"></param>
        private void onDynamicStopServerBtnAsync(EventBase evt) =>
            _ = onDynamicStopServerAsync();

        /// <summary>
        /// Stops the deployment, updating the UI accordingly.
        /// TODO: Cache a list of deployments and/or store a hidden field for requestId.
        /// </summary>
        private async Task onDynamicStopServerAsync()
        {
            // Prepare to stop (UI, status flags, callback unsubs)
            if (IsLogLevelDebug) Debug.Log("onDynamicStopServerAsync");
            hideResultLabels();
            _deploymentsConnectionStopBtn.SetEnabled(false);
            _deploymentsRefreshBtn.SetEnabled(false);
            _deploymentsConnectionStatusLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                "<i>Requesting Stop...</i>", EdgegapWindowMetadata.StatusColors.Processing);

            EdgegapDeploymentsApi deployApi = getDeployApi();
            EdgegapHttpResult<StopActiveDeploymentResult> stopResponse = null;

            try
            {
                stopResponse = await deployApi.StopActiveDeploymentAsync(_deploymentRequestId);

                if (!stopResponse.IsResultCode200)
                {
                    onDynamicStopServerAsyncFail(stopResponse.Error.ErrorMessage);
                    return;
                }

                // ---------
                // 200, but only PENDING deleted (if we create a new one before it's deleted,
                //   user may get hit with max # of deployments reached err)
                _deploymentsConnectionStatusLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                    "<i>Stopping...</i>", EdgegapWindowMetadata.StatusColors.Warn);

                TimeSpan pollIntervalSecs = TimeSpan.FromSeconds(EdgegapWindowMetadata.DEPLOYMENT_STOP_STATUS_POLL_SECONDS);
                stopResponse = await deployApi.AwaitTerminatedDeleteStatusAsync(_deploymentRequestId, pollIntervalSecs);
            }
            finally
            {
                _deploymentsConnectionStopBtn.clickable.clickedWithEventInfo -= onDynamicStopServerBtnAsync;
            }

            bool isStopSuccess = stopResponse.IsResultCode410;
            if (!isStopSuccess)
            {
                onDynamicStopServerAsyncFail(stopResponse.Error.ErrorMessage);
                return;
            }

            // Success: Hide the static row // TODO: Delete the template row, when dynamic
            // clearDeploymentConnections(); // Use this if you don't want to show the last connection info
            string stoppedStr = getConnectionStoppedRichStr();
            _deploymentsStatusLabel.text = ""; // Overrides any previous errs, in case we attempted to created a new deployment while deleting
            setPersistDeploymentsConnectionStatusLabelTxt(stoppedStr);
        }

        private string getConnectionStoppedRichStr() =>
            EdgegapWindowMetadata.WrapRichTextInColor(
                "Stopped", EdgegapWindowMetadata.StatusColors.Error);

        private void onDynamicStopServerAsyncFail(string friendlyErrMsg)
        {
            _deploymentsStatusLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                friendlyErrMsg, EdgegapWindowMetadata.StatusColors.Error);
        }

        /// <summary>Sets and returns `_userExternalIp`, prioritizing local cache</summary>
        private async Task<string> getExternalIpAddress()
        {
            if (!string.IsNullOrEmpty(_userExternalIp))
                return _userExternalIp;

            EdgegapIpApi ipApi = getIpApi();
            EdgegapHttpResult<GetYourPublicIpResult> getYourPublicIpResponseTask = await ipApi.GetYourPublicIp();

            _userExternalIp = getYourPublicIpResponseTask?.Data?.PublicIp;
            Assert.IsTrue(!string.IsNullOrEmpty(_userExternalIp),
                $"Expected getYourPublicIpResponseTask.Data.PublicIp");

            return _userExternalIp;
        }

        #region Api Builders
        private EdgegapDeploymentsApi getDeployApi() => new EdgegapDeploymentsApi( // MIRROR CHANGE: 'new()' not supported in Unity 2020
            EdgegapWindowMetadata.API_ENVIRONMENT,
            _apiTokenInput.value.Trim(),
            EdgegapWindowMetadata.LOG_LEVEL);

        private EdgegapIpApi getIpApi() => new EdgegapIpApi( // MIRROR CHANGE: 'new()' not supported in Unity 2020
            EdgegapWindowMetadata.API_ENVIRONMENT,
            _apiTokenInput.value.Trim(),
            EdgegapWindowMetadata.LOG_LEVEL);

        private EdgegapWizardApi getWizardApi() => new EdgegapWizardApi( // MIRROR CHANGE: 'new()' not supported in Unity 2020
            EdgegapWindowMetadata.API_ENVIRONMENT,
            _apiTokenInput.value.Trim(),
            EdgegapWindowMetadata.LOG_LEVEL);

        private EdgegapAppApi getAppApi() => new EdgegapAppApi( // MIRROR CHANGE: 'new()' not supported in Unity 2020
            EdgegapWindowMetadata.API_ENVIRONMENT,
            _apiTokenInput.value.Trim(),
            EdgegapWindowMetadata.LOG_LEVEL);
        #endregion // Api Builders



        private float ProgressCounter = 0;

        // MIRROR CHANGE: added title parameter for more detailed progress while waiting
        void ShowBuildWorkInProgress(string title, string status)
        {
            EditorUtility.DisplayProgressBar(title, status, ProgressCounter++ / 50);
        }
        // END MIRROR CHANGE

        /// <summary>Build & Push - Legacy from v1, modified for v2</summary>
        private async Task buildAndPushServerAsync()
        {
            if (IsLogLevelDebug) Debug.Log("buildAndPushServerAsync");

            // Legacy Code Start >>

            // SetToolUIState(ToolState.Building);
            SyncObjectWithForm();
            ProgressCounter = 0;

            try
            {
                // check for installation and setup docker file
                if (!await EdgegapBuildUtils.DockerSetupAndInstallationCheck())
                {
                    onBuildPushError("Docker installation not found. " +
                        "Docker can be downloaded from:\n\nhttps://www.docker.com/");
                    return;
                }

                // MIRROR CHANGE
                // make sure Linux build target is installed before attemping to build.
                // if it's not installed, tell the user about it.
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
                {
                    onBuildPushError($"Linux Build Support is missing.\n\nPlease open Unity Hub -> Installs -> Unity {Application.unityVersion} -> Add Modules -> Linux Build Support (IL2CPP & Mono & Dedicated Server) -> Install\n\nAfterwards restart Unity!");
                    return;
                }
                // END MIRROR CHANGE

                if (!EdgegapWindowMetadata.SKIP_SERVER_BUILD_WHEN_PUSHING)
                {
                    // create server build
                    BuildReport buildResult = EdgegapBuildUtils.BuildServer();
                    if (buildResult.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    {
                        onBuildPushError("Edgegap build failed");
                        return;
                    }
                }
                else
                    Debug.LogWarning(nameof(EdgegapWindowMetadata.SKIP_SERVER_BUILD_WHEN_PUSHING));

                string registry = _containerRegistryUrlInput.value;
                string imageName = _containerImageRepositoryInput.value;
                string tag = _containerNewTagVersionInput.value;

                // MIRROR CHANGE ///////////////////////////////////////////////
                // registry, repository and tag can not contain whitespaces.
                // otherwise the docker command will throw an error:
                // "ERROR: "docker buildx build" requires exactly 1 argument."
                // catch this early and notify the user immediately.
                if (registry.Contains(" "))
                {
                    onBuildPushError($"Container Registry is not allowed to contain whitespace: '{registry}'");
                    return;
                }

                if (imageName.Contains(" "))
                {
                    onBuildPushError($"Image Repository is not allowed to contain whitespace: '{imageName}'");
                    return;
                }

                if (tag.Contains(" "))
                {
                    onBuildPushError($"Tag is not allowed to contain whitespace: '{tag}'");
                    return;
                }
                // END MIRROR CHANGE ///////////////////////////////////////////

                // // increment tag for quicker iteration // TODO? `_autoIncrementTag` !exists in V2.
                // if (_autoIncrementTag)
                // {
                //     tag = EdgegapBuildUtils.IncrementTag(tag);
                // }

                // create docker image
                if (!EdgegapWindowMetadata.SKIP_DOCKER_IMAGE_BUILD_WHEN_PUSHING)
                {
                    // MIRROR CHANGE: CROSS PLATFORM BUILD SUPPORT
                    // await EdgegapBuildUtils.DockerBuild(
                    //     registry,
                    //     imageName,
                    //     tag,
                    //     ShowBuildWorkInProgress);

                    await EdgegapBuildUtils.RunCommand_DockerBuild(registry, imageName, tag, status => ShowBuildWorkInProgress("Building Docker Image", status));

                }
                else
                    Debug.LogWarning(nameof(EdgegapWindowMetadata.SKIP_DOCKER_IMAGE_BUILD_WHEN_PUSHING));

                // (v2) Login to registry
                bool isContainerLoginSuccess = await EdgegapBuildUtils.LoginContainerRegistry(
                    _containerRegistryUrlInput.value,
                    _containerUsernameInput.value,
                    _containerTokenInput.value,
                    status => ShowBuildWorkInProgress("Logging into container registry.", status)); // MIRROR CHANGE: DETAILED LOGGING

                if (!isContainerLoginSuccess)
                {
                    onBuildPushError("Unable to login to docker registry. " +
                        "Make sure your registry url + username are correct. " +
                        $"See doc:\n\n{EdgegapWindowMetadata.EDGEGAP_DOC_BTN_HOW_TO_LOGIN_VIA_CLI_URL}");
                    return;
                }

                // MIRROR CHANGE: DETAILED DOCKER PUSH ERROR HANDLING
                // push docker image
                (bool isPushSuccess, string error) = await EdgegapBuildUtils.RunCommand_DockerPush(registry, imageName, tag, status => ShowBuildWorkInProgress("Uploading Docker Image (this may take a while)", status));
                if (!isPushSuccess)
                {
                    // catch common issues with detailed solutions
                    if (error.Contains("Cannot connect to the Docker daemon"))
                    {
                        onBuildPushError($"{error}\nTo solve this, you can install and run Docker Desktop from:\n\nhttps://www.docker.com/products/docker-desktop");
                        return;
                    }

                    if (error.Contains("unauthorized to access repository"))
                    {
                        onBuildPushError($"Docker authorization failed:\n\n{error}\nTo solve this, you can open a terminal and enter 'docker login {registry}', then enter your credentials.");
                        return;
                    }

                    // project not found?
                    if (Regex.IsMatch(error, @".*project .* not found.*", RegexOptions.IgnoreCase))
                    {
                        onBuildPushError($"{error}\nTo solve this, make sure that Image Repository is 'project/game' where 'project' is from the Container Registry page on the Edgegap website.");
                        return;
                    }

                    // otherwise show generic error message
                    onBuildPushError("Unable to push docker image to registry. " +
                        $"Make sure your {registry} registry url + username are correct. " +
                        $"See doc:\n\n{EdgegapWindowMetadata.EDGEGAP_DOC_BTN_HOW_TO_LOGIN_VIA_CLI_URL}");
                    return;
                }
                // END MIRROR CHANGE

                // update edgegap server settings for new tag
                ShowBuildWorkInProgress("Build and Push", "Updating server info on Edgegap");
                EdgegapAppApi appApi = getAppApi();

                AppPortsData[] ports =
                {
                    new AppPortsData() // MIRROR CHANGE: 'new()' not supported in Unity 2020
                    {
                        Port = int.Parse(_containerPortNumInput.value), // OnInputChange clamps + validates,
                        ProtocolStr = _containerTransportTypeEnumInput.value.ToString(),
                    },
                };

                UpdateAppVersionRequest updateAppVerReq = new UpdateAppVersionRequest(_appNameInput.value) // MIRROR CHANGE: 'new()' not supported in Unity 2020
                {
                    VersionName = _containerNewTagVersionInput.value,
                    DockerImage = imageName,
                    DockerRepository = registry,
                    DockerTag = tag,
                    PrivateUsername = _containerUsernameInput.value,
                    PrivateToken = _containerTokenInput.value,
                    Ports = ports,
                };

                EdgegapHttpResult<UpsertAppVersionResult> updateAppVersionResult = await appApi.UpsertAppVersion(updateAppVerReq);

                if (updateAppVersionResult.HasErr)
                {
                    onBuildPushError($"Unable to update docker tag/version:\n{updateAppVersionResult.Error.ErrorMessage}");
                    return;
                }

                // cleanup
                onBuildAndPushSuccess(tag);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError(ex);

                string errMsg = "Edgegapbuild and push failed";
                if (ex.Message.Contains("docker daemon is not running"))
                {
                    errMsg += ":\nDocker is installed, but the daemon/app (such as `Docker Desktop`) is not running. " +
                        "Please start Docker Desktop and try again.";
                }
                else
                    errMsg += $":\n{ex.Message}";

                onBuildPushError(errMsg);
            }
            // MIRROR CHANGE: always clear otherwise it gets stuck there forever!
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            // END MIRROR CHANGE
        }

        private void onBuildAndPushSuccess(string tag)
        {
            // _containerImageTag = tag; // TODO?
            syncFormWithObjectStatic();
            EditorUtility.ClearProgressBar();

            _containerBuildAndPushResultLabel.text = $"Success ({tag})";
            _containerBuildAndPushResultLabel.visible = true;

            Debug.Log("Server built and pushed successfully");
        }

        /// <summary>(v2) Docker cmd error, detected by "ERROR" in log stream.</summary>
        private void onBuildPushError(string msg)
        {
            EditorUtility.ClearProgressBar();
            _containerBuildAndPushResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                "Error", EdgegapWindowMetadata.StatusColors.Error);
            EditorUtility.DisplayDialog("Error", msg, "Ok"); // Show this last! It's blocking!
        }


        #region Persistence Helpers
        /// <summary>
        /// Load from EditorPrefs, persisting from a previous session, if the field is empty
        /// - ApiToken; !persisted via ViewDataKey so we don't save plaintext
        /// - DeploymentRequestId
        /// - DeploymentConnectionUrl
        /// - DeploymentConnectionStatus
        /// </summary>
        private void loadPersistentDataFromEditorPrefs()
        {
            // ApiToken
            if (string.IsNullOrEmpty(_apiTokenInput.value))
                setMaskedApiTokenFromEditorPrefs();

            // DeploymentRequestId
            if (string.IsNullOrEmpty(_deploymentRequestId))
                _deploymentRequestId = EditorPrefs.GetString(EdgegapWindowMetadata.DEPLOYMENT_REQUEST_ID_KEY_STR);

            // DeploymentConnectionUrl
            if (string.IsNullOrEmpty(_deploymentsConnectionUrlReadonlyInput.text))
            {
                _deploymentsConnectionUrlReadonlyInput.value = getDeploymentsConnectionUrlLabelTxt();
                bool hasVal = !string.IsNullOrEmpty(_deploymentsConnectionUrlReadonlyInput.value);
                if (hasVal && string.IsNullOrEmpty(_deploymentRequestId))
                {
                    // Fallback -- if no requestId, we can actually get it from the url since we have this (desync)
                    _deploymentRequestId = _deploymentsConnectionUrlReadonlyInput.value.Split('.')[0];
                    EditorPrefs.SetString(EdgegapWindowMetadata.DEPLOYMENT_REQUEST_ID_KEY_STR, _deploymentRequestId);
                }
                // TODO (Optional): Show a status label to remind these are cached vals; refresh for live?
            }

            // DeploymentConnectionStatus
            if (string.IsNullOrEmpty(_deploymentsConnectionStatusLabel.text) || _deploymentsConnectionStatusLabel.text == "Unknown")
                _deploymentsConnectionStatusLabel.text = getDeploymentsConnectionStatusLabelTxt();
        }

        /// <summary>Set Label -> Persist to EditorPrefs</summary>
        /// <param name="newDomainWithPort"></param>
        private void setPersistDeploymentsConnectionUrlLabelTxt(string newDomainWithPort)
        {
            _deploymentsConnectionUrlReadonlyInput.value = newDomainWithPort;
            EditorPrefs.SetString(EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_URL_KEY_STR, newDomainWithPort);
        }

        /// <summary>Get persistent data fromEditorPrefs</summary>
        private string getDeploymentsConnectionUrlLabelTxt() =>
            EditorPrefs.GetString(EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_URL_KEY_STR);

        /// <summary>Set label -> persist to EditorPrefs</summary>
        /// <param name="newStatus"></param>
        private void setPersistDeploymentsConnectionStatusLabelTxt(string newStatus)
        {
            _deploymentsConnectionStatusLabel.text = newStatus;
            EditorPrefs.SetString(EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_STATUS_KEY_STR, newStatus);
        }

        /// <summary>Get persistent data fromEditorPrefs</summary>
        private string getDeploymentsConnectionStatusLabelTxt() =>
            EditorPrefs.GetString(EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_STATUS_KEY_STR);

        /// <summary>Set to base64 -> Save to EditorPrefs</summary>
        /// <param name="value"></param>
        private void persistUnmaskedApiToken(string value)
        {
            EditorPrefs.SetString(
                EdgegapWindowMetadata.API_TOKEN_KEY_STR,
                Base64Encode(value));
        }

        /// <summary>
        /// Get apiToken from EditorPrefs -> Base64 Decode -> Set to apiTokenInput
        /// </summary>
        private void setMaskedApiTokenFromEditorPrefs()
        {
            string apiTokenBase64Str = EditorPrefs.GetString(
                EdgegapWindowMetadata.API_TOKEN_KEY_STR, null);

            if (apiTokenBase64Str == null)
                return;

            string decodedApiToken = Base64Decode(apiTokenBase64Str);
            _apiTokenInput.SetValueWithoutNotify(decodedApiToken);
        }
        #endregion // Persistence Helpers
    }
}
