// MIRROR CHANGE: disable this completely. otherwise InitUIElements can still throw NRE.
/*
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IO.Swagger.Model;
using UnityEditor.Build.Reporting;
using Application = UnityEngine.Application;

namespace Edgegap
{
    public class EdgegapWindow : EditorWindow
    {
        // MIRROR CHANGE: create HTTPClient in-place to avoid InvalidOperationExceptions when reusing
        // static readonly HttpClient _httpClient = new HttpClient();
        // END MIRROR CHANGE

        const string EditorDataSerializationName = "EdgegapSerializationData";
        const int ServerStatusCronjobIntervalMs = 10000; // Interval at which the server status is updated

        // MIRROR CHANGE
        // get the path of this .cs file so we don't need to hardcode paths to
        // the .uxml and .uss files:
        // https://forum.unity.com/threads/too-many-hard-coded-paths-in-the-templates-and-documentation.728138/
        // this way users can move this folder without breaking UIToolkit paths.
        internal string StylesheetPath =>
            Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
        // END MIRROR CHANGE

        readonly System.Timers.Timer _updateServerStatusCronjob = new System.Timers.Timer(ServerStatusCronjobIntervalMs);

        [SerializeField] string _userExternalIp;
        [SerializeField] string _apiKey;
        [SerializeField] ApiEnvironment _apiEnvironment;
        [SerializeField] string _appName;
        [SerializeField] string _appVersionName;
        [SerializeField] string _deploymentRequestId;

        [SerializeField] string _containerRegistry;
        [SerializeField] string _containerImageRepo;
        [SerializeField] string _containerImageTag;
        [SerializeField] bool _autoIncrementTag = true;


        VisualTreeAsset _visualTree;
        bool _shouldUpdateServerStatus = false;

        // Interactable elements
        EnumField _apiEnvironmentSelect;
        TextField _apiKeyInput;
        TextField _appNameInput;
        TextField _appVersionNameInput;
        TextField _containerRegistryInput;
        TextField _containerImageRepoInput;
        TextField _containerImageTagInput;
        Toggle _autoIncrementTagInput;
        Button _connectionButton;
        Button _serverActionButton;
        Button _documentationBtn;
        Button _buildAndPushServerBtn;

        // Readonly elements
        Label _connectionStatusLabel;
        VisualElement _serverDataContainer;

        // server data manager
        StyleSheet _serverDataStylesheet;
        List<VisualElement> _serverDataContainers = new List<VisualElement>();

        [Obsolete("See EdgegapWindowV2.ShowEdgegapToolWindow()")]
        // [MenuItem("Edgegap/Server Management")]
        public static void ShowEdgegapToolWindow()
        {
            EdgegapWindow window = GetWindow<EdgegapWindow>();
            window.titleContent = new GUIContent("Edgegap Hosting"); // MIRROR CHANGE
        }

        protected void OnEnable()
        {
            // Set root VisualElement and style
            // BEGIN MIRROR CHANGE
            _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{StylesheetPath}/EdgegapWindow.uxml");
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{StylesheetPath}/EdgegapWindow.uss");
            _serverDataStylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{StylesheetPath}/EdgegapServerData.uss");
            // END MIRROR CHANGE
            rootVisualElement.styleSheets.Add(styleSheet);

            LoadToolData();

            if (string.IsNullOrWhiteSpace(_userExternalIp))
            {
                _userExternalIp = GetExternalIpAddress();
            }
        }

        protected void Update()
        {
            if (_shouldUpdateServerStatus)
            {
                _shouldUpdateServerStatus = false;
                UpdateServerStatus();
            }
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            _visualTree.CloneTree(rootVisualElement);

            InitUIElements();
            SyncFormWithObject();

            bool hasActiveDeployment = !string.IsNullOrEmpty(_deploymentRequestId);

            if (hasActiveDeployment)
            {
                RestoreActiveDeployment();
            }
            else
            {
                DisconnectCallback();
            }
        }

        protected void OnDestroy()
        {
            bool deploymentActive = !string.IsNullOrEmpty(_deploymentRequestId);

            if (deploymentActive)
            {
                EditorUtility.DisplayDialog(
                        "Warning",
                        $"You have an active deployment ({_deploymentRequestId}) that won't be stopped automatically.",
                        "Ok"
                    );
            }
        }

        protected void OnDisable()
        {
            SyncObjectWithForm();
            SaveToolData();
            DeregisterServerDataContainer(_serverDataContainer);
        }

        /// <summary>
        /// Binds the form inputs to the associated variables and initializes the inputs as required.
        /// Requires the VisualElements to be loaded before this call. Otherwise, the elements cannot be found.
        /// </summary>
        void InitUIElements()
        {
            _apiEnvironmentSelect = rootVisualElement.Q<EnumField>("environmentSelect");
            _apiKeyInput = rootVisualElement.Q<TextField>("apiKey");
            _appNameInput = rootVisualElement.Q<TextField>("appName");
            _appVersionNameInput = rootVisualElement.Q<TextField>("appVersionName");

            _containerRegistryInput = rootVisualElement.Q<TextField>("containerRegistry");
            _containerImageRepoInput = rootVisualElement.Q<TextField>("containerImageRepo");
            _containerImageTagInput = rootVisualElement.Q<TextField>("tag");
            _autoIncrementTagInput = rootVisualElement.Q<Toggle>("autoIncrementTag");

            _connectionButton = rootVisualElement.Q<Button>("connectionBtn");
            _serverActionButton = rootVisualElement.Q<Button>("serverActionBtn");
            _documentationBtn = rootVisualElement.Q<Button>("documentationBtn");
            _buildAndPushServerBtn = rootVisualElement.Q<Button>("buildAndPushBtn");
            _buildAndPushServerBtn.clickable.clicked += BuildAndPushServer;

            _connectionStatusLabel = rootVisualElement.Q<Label>("connectionStatusLabel");
            _serverDataContainer = rootVisualElement.Q<VisualElement>("serverDataContainer");

            // Load initial server data UI element and register for updates.
            VisualElement serverDataElement = GetServerDataVisualTree();
            RegisterServerDataContainer(serverDataElement);
            _serverDataContainer.Clear();
            _serverDataContainer.Add(serverDataElement);

            _documentationBtn.clickable.clicked += OpenDocumentationCallback;

            // Init the ApiEnvironment dropdown
            _apiEnvironmentSelect.Init(ApiEnvironment.Console);
        }

        /// <summary>
        /// With a call to an external resource, determines the current user's public IP address.
        /// </summary>
        /// <returns>External IP address</returns>
        string GetExternalIpAddress()
        {
            string externalIpString = new WebClient()
                .DownloadString("http://icanhazip.com")
                .Replace("\\r\\n", "")
                .Replace("\\n", "")
                .Trim();
            IPAddress externalIp = IPAddress.Parse(externalIpString);

            return externalIp.ToString();
        }

        void OpenDocumentationCallback()
        {
            // MIRROR CHANGE
            // ApiEnvironment selectedApiEnvironment = (ApiEnvironment)_apiEnvironmentSelect.value;
            // string documentationUrl = selectedApiEnvironment.GetDocumentationUrl();
            //
            // if (!string.IsNullOrEmpty(documentationUrl))
            // {
            //     UnityEngine.Application.OpenURL(documentationUrl);
            // }
            // else
            // {
            //     string apiEnvName = Enum.GetName(typeof(ApiEnvironment), selectedApiEnvironment);
            //     Debug.LogWarning($"Could not open documentation for api environment {apiEnvName}: No documentation URL.");
            // }

            // link to the easiest documentation
            Application.OpenURL("https://mirror-networking.gitbook.io/docs/hosting/edgegap-hosting-plugin-guide");
            // END MIRROR CHANGE
        }

        void ConnectCallback()
        {
            ApiEnvironment selectedApiEnvironment = (ApiEnvironment)_apiEnvironmentSelect.value;
            string selectedAppName = _appNameInput.value;
            string selectedVersionName = _appVersionNameInput.value;
            string selectedApiKey = _apiKeyInput.value;

            bool validAppName = !string.IsNullOrEmpty(selectedAppName) && !string.IsNullOrWhiteSpace(selectedAppName);
            bool validVersionName = !string.IsNullOrEmpty(selectedVersionName) && !string.IsNullOrWhiteSpace(selectedVersionName);
            bool validApiKey = selectedApiKey.StartsWith("token ");

            if (validAppName && validVersionName && validApiKey)
            {
                string apiKeyValue = selectedApiKey.Substring(6);
                Connect(selectedApiEnvironment, selectedAppName, selectedVersionName, apiKeyValue);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Could not connect - Invalid data",
                    "The data provided is invalid. " +
                    "Make sure every field is filled, and that you provide your complete Edgegap API token " +
                    "(including the \"token\" part).",
                    "Ok"
                );
            }
        }

        // MIRROR CHANGE: create HTTPClient in-place to avoid InvalidOperationExceptions when reusing
        HttpClient CreateHttpClient()
        {
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(_apiEnvironment.GetApiUrl());
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            string token = _apiKeyInput.value.Substring(6);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
            return httpClient;
        }
        // END MIRROR CHANGE

        async void Connect(
            ApiEnvironment selectedApiEnvironment,
            string selectedAppName,
            string selectedAppVersionName,
            string selectedApiTokenValue
        )
        {
            SetToolUIState(ToolState.Connecting);

            // Make HTTP request
            HttpClient _httpClient = CreateHttpClient(); // MIRROR CHANGE: create HTTPClient in-place to avoid InvalidOperationExceptions when reusing
            string path = $"/v1/app/{selectedAppName}/version/{selectedAppVersionName}"; // MIRROR CHANGE: use selectedAppName and selectedAppVersionName instead of _appName & _appVersionName
            HttpResponseMessage response = await _httpClient.GetAsync(path);

            if (response.IsSuccessStatusCode)
            {
                SyncObjectWithForm();
                SetToolUIState(ToolState.Connected);
            }
            else
            {
                int status = (int)response.StatusCode;
                string title;
                string message;

                if (status == 401)
                {
                    string apiEnvName = Enum.GetName(typeof(ApiEnvironment), selectedApiEnvironment);
                    title = "Invalid credentials";
                    message = $"Could not find an Edgegap account with this API key for the {apiEnvName} environment.";
                }
                else if (status == 404)
                {
                    title = "App not found";
                    message = $"Could not find app {selectedAppName} with version {selectedAppVersionName}.";
                }
                else
                {
                    title = "Oops";
                    message = $"There was an error while connecting you to the Edgegap API. Please try again later.";
                }

                EditorUtility.DisplayDialog(title, message, "Ok");
                SetToolUIState(ToolState.Disconnected);
            }
        }

        void DisconnectCallback()
        {
            if (string.IsNullOrEmpty(_deploymentRequestId))
            {
                SetToolUIState(ToolState.Disconnected);
            }
            else
            {
                EditorUtility.DisplayDialog("Cannot disconnect", "Make sure no server is running in the Edgegap tool before disconnecting", "Ok");
            }
        }

        float ProgressCounter = 0;

        // MIRROR CHANGE: added title parameter for more detailed progress while waiting
        void ShowBuildWorkInProgress(string title, string status)
        {
            EditorUtility.DisplayProgressBar(title, status, ProgressCounter++ / 50);
        }
        // END MIRROR CHANGE

        async void BuildAndPushServer()
        {
            SetToolUIState(ToolState.Building);

            SyncObjectWithForm();
            ProgressCounter = 0;
            Action<string> onError = (msg) =>
            {
                EditorUtility.DisplayDialog("Error", msg, "Ok");
                SetToolUIState(ToolState.Connected);
            };

            try
            {
                // check for installation and setup docker file
                if (!await EdgegapBuildUtils.DockerSetupAndInstallationCheck())
                {
                    onError("Docker installation not found. Docker can be downloaded from:\n\nhttps://www.docker.com/");
                    return;
                }

                // MIRROR CHANGE
                // make sure Linux build target is installed before attemping to build.
                // if it's not installed, tell the user about it.
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
                {
                    onError($"Linux Build Support is missing.\n\nPlease open Unity Hub -> Installs -> Unity {Application.unityVersion} -> Add Modules -> Linux Build Support (IL2CPP & Mono & Dedicated Server) -> Install\n\nAfterwards restart Unity!");
                    return;
                }
                // END MIRROR CHANGE

                // create server build
                BuildReport buildResult = EdgegapBuildUtils.BuildServer();
                if (buildResult.summary.result != BuildResult.Succeeded)
                {
                    onError("Edgegap build failed, please check the Unity console logs.");
                    return;
                }

                string registry = _containerRegistry;
                string imageName = _containerImageRepo;
                string tag = _containerImageTag;

                // MIRROR CHANGE ///////////////////////////////////////////////
                // registry, repository and tag can not contain whitespaces.
                // otherwise the docker command will throw an error:
                // "ERROR: "docker buildx build" requires exactly 1 argument."
                // catch this early and notify the user immediately.
                if (registry.Contains(" "))
                {
                    onError($"Container Registry is not allowed to contain whitespace: '{registry}'");
                    return;
                }

                if (imageName.Contains(" "))
                {
                    onError($"Image Repository is not allowed to contain whitespace: '{imageName}'");
                    return;
                }

                if (tag.Contains(" "))
                {
                    onError($"Tag is not allowed to contain whitespace: '{tag}'");
                    return;
                }
                // END MIRROR CHANGE ///////////////////////////////////////////

                // increment tag for quicker iteration
                if (_autoIncrementTag)
                {
                    tag = EdgegapBuildUtils.IncrementTag(tag);
                }

                // create docker image
                await EdgegapBuildUtils.RunCommand_DockerBuild(registry, imageName, tag, status => ShowBuildWorkInProgress("Building Docker Image", status));

                SetToolUIState(ToolState.Pushing);

                // push docker image
                (bool result, string error) = await EdgegapBuildUtils.RunCommand_DockerPush(registry, imageName, tag, status => ShowBuildWorkInProgress("Uploading Docker Image (this may take a while)", status));
                if (!result)
                {
                    // catch common issues with detailed solutions
                    if (error.Contains("Cannot connect to the Docker daemon"))
                    {
                        onError($"{error}\nTo solve this, you can install and run Docker Desktop from:\n\nhttps://www.docker.com/products/docker-desktop");
                        return;
                    }

                    if (error.Contains("unauthorized to access repository"))
                    {
                        onError($"Docker authorization failed:\n\n{error}\nTo solve this, you can open a terminal and enter 'docker login {registry}', then enter your credentials.");
                        return;
                    }

                    // project not found?
                    if (Regex.IsMatch(error, @".*project .* not found.*", RegexOptions.IgnoreCase))
                    {
                        onError($"{error}\nTo solve this, make sure that Image Repository is 'project/game' where 'project' is from the Container Registry page on the Edgegap website.");
                        return;
                    }

                    // otherwise show generic error message
                    onError($"Unable to push docker image to registry. Please make sure you're logged in to {registry} and check the following error:\n\n{error}");
                    return;
                }

                // update edgegap server settings for new tag
                ShowBuildWorkInProgress("Build and Push", "Updating server info on Edgegap");
                await UpdateAppTagOnEdgegap(tag);

                // cleanup
                _containerImageTag = tag;
                SyncFormWithObject();
                SetToolUIState(ToolState.Connected);

                Debug.Log("Server built and pushed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                onError($"Edgegap build and push failed with Error: {ex}");
            }
            finally
            {
                // MIRROR CHANGE: always clear otherwise it gets stuck there forever!
                EditorUtility.ClearProgressBar();
            }
        }

        async Task UpdateAppTagOnEdgegap(string newTag)
        {
            string path = $"/v1/app/{_appName}/version/{_appVersionName}";

            // Setup post data
            AppVersionUpdatePatchData updatePatchData = new AppVersionUpdatePatchData { DockerImage = _containerImageRepo, DockerRegistry = _containerRegistry, DockerTag = newTag };
            string json = JsonConvert.SerializeObject(updatePatchData);
            StringContent patchData = new StringContent(json, Encoding.UTF8, "application/json");

            // Make HTTP request
            HttpClient _httpClient = CreateHttpClient(); // MIRROR CHANGE: create HTTPClient in-place to avoid InvalidOperationExceptions when reusing
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), path);
            request.Content = patchData;
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Could not update Edgegap server tag. Got {(int)response.StatusCode} with response:\n{content}");
            }
        }

        async void StartServerCallback()
        {
            SetToolUIState(ToolState.ProcessingDeployment); // Prevents being called multiple times.

            const string path = "/v1/deploy";

            // Setup post data
            DeployPostData deployPostData = new DeployPostData(_appName, _appVersionName, new List<string> { _userExternalIp });
            string json = JsonConvert.SerializeObject(deployPostData);
            StringContent postData = new StringContent(json, Encoding.UTF8, "application/json");

            // Make HTTP request
            HttpClient _httpClient = CreateHttpClient(); // MIRROR CHANGE: create HTTPClient in-place to avoid InvalidOperationExceptions when reusing
            HttpResponseMessage response = await _httpClient.PostAsync(path, postData);
            string content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Parse response
                Deployment parsedResponse = JsonConvert.DeserializeObject<Deployment>(content);

                _deploymentRequestId = parsedResponse.RequestId;

                UpdateServerStatus();
                StartServerStatusCronjob();
            }
            else
            {
                Debug.LogError($"Could not start Edgegap server. Got {(int)response.StatusCode} with response:\n{content}");
                SetToolUIState(ToolState.Connected);
            }
        }

        async void StopServerCallback()
        {
            string path = $"/v1/stop/{_deploymentRequestId}";

            // Make HTTP request
            HttpClient _httpClient = CreateHttpClient(); // MIRROR CHANGE: create HTTPClient in-place to avoid InvalidOperationExceptions when reusing
            HttpResponseMessage response = await _httpClient.DeleteAsync(path);

            if (response.IsSuccessStatusCode)
            {
                UpdateServerStatus();
                SetToolUIState(ToolState.ProcessingDeployment);
            }
            else
            {
                // Parse response
                string content = await response.Content.ReadAsStringAsync();

                Debug.LogError($"Could not stop Edgegap server. Got {(int)response.StatusCode} with response:\n{content}");
            }
        }

        void StartServerStatusCronjob()
        {
            _updateServerStatusCronjob.Elapsed += (sourceObject, elaspedEvent) => _shouldUpdateServerStatus = true;
            _updateServerStatusCronjob.AutoReset = true;
            _updateServerStatusCronjob.Start();
        }

        void StopServerStatusCronjob() => _updateServerStatusCronjob.Stop();

        async void UpdateServerStatus()
        {
            Status serverStatusResponse = await FetchServerStatus();

            ToolState toolState;
            ServerStatus serverStatus = serverStatusResponse.GetServerStatus();

            if (serverStatus == ServerStatus.Terminated)
            {
                SetServerData(null, _apiEnvironment);

                if (_updateServerStatusCronjob.Enabled)
                {
                    StopServerStatusCronjob();
                }

                _deploymentRequestId = null;
                toolState = ToolState.Connected;
            }
            else
            {
                SetServerData(serverStatusResponse, _apiEnvironment);

                if (serverStatus == ServerStatus.Ready || serverStatus == ServerStatus.Error)
                {
                    toolState = ToolState.DeploymentRunning;
                }
                else
                {
                    toolState = ToolState.ProcessingDeployment;
                }
            }

            SetToolUIState(toolState);
        }

        async Task<Status> FetchServerStatus()
        {
            string path = $"/v1/status/{_deploymentRequestId}";

            // Make HTTP request
            HttpClient _httpClient = CreateHttpClient(); // MIRROR CHANGE: create HTTPClient in-place to avoid InvalidOperationExceptions when reusing
            HttpResponseMessage response = await _httpClient.GetAsync(path);

            // Parse response
            string content = await response.Content.ReadAsStringAsync();

            Status parsedData;

            if (response.IsSuccessStatusCode)
            {
                parsedData = JsonConvert.DeserializeObject<Status>(content);
            }
            else
            {
                if ((int)response.StatusCode == 400)
                {
                    Debug.LogError("The deployment that was active in the tool is now unreachable. Considering it Terminated.");
                    parsedData = new Status() { CurrentStatus = ServerStatus.Terminated.GetLabelText() };
                }
                else
                {
                    Debug.LogError(
                        $"Could not fetch status of Edgegap deployment {_deploymentRequestId}. " +
                        $"Got {(int)response.StatusCode} with response:\n{content}"
                    );
                    parsedData = new Status() { CurrentStatus = ServerStatus.NA.GetLabelText() };
                }
            }

            return parsedData;
        }

        void RestoreActiveDeployment()
        {
            ConnectCallback();

            _shouldUpdateServerStatus = true;
            StartServerStatusCronjob();
        }

        void SyncObjectWithForm()
        {
            if (_apiKeyInput == null) return; // MIRROR CHANGE: fix NRE when this is called before UI elements were assgned

            _apiKey = _apiKeyInput.value;
            _apiEnvironment = (ApiEnvironment)_apiEnvironmentSelect.value;
            _appName = _appNameInput.value;
            _appVersionName = _appVersionNameInput.value;

            // MIRROR CHANGE ///////////////////////////////////////////////////
            // registry, repository and tag can not contain whitespaces.
            // otherwise it'll throw an error:
            // "ERROR: "docker buildx build" requires exactly 1 argument."
            // trim whitespace in case users accidentally added some.
            _containerRegistry = _containerRegistryInput.value.Trim();
            _containerImageTag = _containerImageTagInput.value.Trim();
            _containerImageRepo = _containerImageRepoInput.value.Trim();
            // END MIRROR CHANGE ///////////////////////////////////////////////

            _autoIncrementTag = _autoIncrementTagInput.value;
        }

        void SyncFormWithObject()
        {
            _apiKeyInput.value = _apiKey;
            _apiEnvironmentSelect.value = _apiEnvironment;
            _appNameInput.value = _appName;
            _appVersionNameInput.value = _appVersionName;

            _containerRegistryInput.value = _containerRegistry;
            _containerImageTagInput.value = _containerImageTag;
            _containerImageRepoInput.value = _containerImageRepo;
            _autoIncrementTagInput.value = _autoIncrementTag;
        }

        void SetToolUIState(ToolState toolState)
        {
            SetConnectionInfoUI(toolState);
            SetConnectionButtonUI(toolState);
            SetServerActionUI(toolState);
            SetDockerRepoInfoUI(toolState);
        }

        void SetDockerRepoInfoUI(ToolState toolState)
        {
            bool connected = toolState.CanStartDeployment();
            _containerRegistryInput.SetEnabled(connected);
            _autoIncrementTagInput.SetEnabled(connected);
            _containerImageRepoInput.SetEnabled(connected);
            _containerImageTagInput.SetEnabled(connected);

        }

        void SetConnectionInfoUI(ToolState toolState)
        {
            bool canEditConnectionInfo = toolState.CanEditConnectionInfo();

            _apiKeyInput.SetEnabled(canEditConnectionInfo);
            _apiEnvironmentSelect.SetEnabled(canEditConnectionInfo);
            _appNameInput.SetEnabled(canEditConnectionInfo);
            _appVersionNameInput.SetEnabled(canEditConnectionInfo);

        }

        void SetConnectionButtonUI(ToolState toolState)
        {
            bool canConnect = toolState.CanConnect();
            bool canDisconnect = toolState.CanDisconnect();

            _connectionButton.SetEnabled(canConnect || canDisconnect);

            // A bit dirty, but ensures the callback is not bound multiple times on the button.
            _connectionButton.clickable.clicked -= ConnectCallback;
            _connectionButton.clickable.clicked -= DisconnectCallback;

            if (canConnect || toolState == ToolState.Connecting)
            {
                _connectionButton.text = "Connect";
                _connectionStatusLabel.text = "Awaiting connection";
                _connectionStatusLabel.RemoveFromClassList("text--success");
                _connectionButton.clickable.clicked += ConnectCallback;
            }
            else
            {
                _connectionButton.text = "Disconnect";
                _connectionStatusLabel.text = "Connected";
                _connectionStatusLabel.AddToClassList("text--success");
                _connectionButton.clickable.clicked += DisconnectCallback;
            }
        }

        void SetServerActionUI(ToolState toolState)
        {
            bool canStartDeployment = toolState.CanStartDeployment();
            bool canStopDeployment = toolState.CanStopDeployment();

            // A bit dirty, but ensures the callback is not bound multiple times on the button.
            _serverActionButton.clickable.clicked -= StartServerCallback;
            _serverActionButton.clickable.clicked -= StopServerCallback;

            _serverActionButton.SetEnabled(canStartDeployment || canStopDeployment);

            _buildAndPushServerBtn.SetEnabled(canStartDeployment);

            if (canStopDeployment)
            {
                _serverActionButton.text = "Stop Server";
                _serverActionButton.clickable.clicked += StopServerCallback;
            }
            else
            {
                _serverActionButton.text = "Start Server";
                _serverActionButton.clickable.clicked += StartServerCallback;
            }
        }

        // server data manager /////////////////////////////////////////////////
        public void RegisterServerDataContainer(VisualElement serverDataContainer)
        {
            _serverDataContainers.Add(serverDataContainer);
        }

        public void DeregisterServerDataContainer(VisualElement serverDataContainer)
        {
            _serverDataContainers.Remove(serverDataContainer);
        }

        public void SetServerData(Status serverData, ApiEnvironment apiEnvironment)
        {
            EdgegapServerDataManager._serverData = serverData;
            RefreshServerDataContainers();
        }

        public Label GetHeader(string text)
        {
            Label header = new Label(text);
            header.AddToClassList("label__header");

            return header;
        }

        public VisualElement GetHeaderRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("row__port-table");
            row.AddToClassList("label__header");

            row.Add(new Label("Name"));
            row.Add(new Label("External"));
            row.Add(new Label("Internal"));
            row.Add(new Label("Protocol"));
            row.Add(new Label("Link"));

            return row;
        }

        public VisualElement GetRowFromPortResponse(PortMapping port)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("row__port-table");
            row.AddToClassList("focusable");


            row.Add(new Label(port.Name));
            row.Add(new Label(port.External.ToString()));
            row.Add(new Label(port.Internal.ToString()));
            row.Add(new Label(port.Protocol));
            row.Add(GetCopyButton("Copy", port.Link));

            return row;
        }

        public Button GetCopyButton(string btnText, string copiedText)
        {
            Button copyBtn = new Button();
            copyBtn.text = btnText;
            copyBtn.clickable.clicked += () => GUIUtility.systemCopyBuffer = copiedText;

            return copyBtn;
        }

        public Button GetLinkButton(string btnText, string targetUrl)
        {
            Button copyBtn = new Button();
            copyBtn.text = btnText;
            copyBtn.clickable.clicked += () => UnityEngine.Application.OpenURL(targetUrl);

            return copyBtn;
        }
        public Label GetInfoText(string innerText)
        {
            Label infoText = new Label(innerText);
            infoText.AddToClassList("label__info-text");

            return infoText;
        }

        VisualElement GetStatusSection()
        {
            ServerStatus serverStatus = EdgegapServerDataManager._serverData.GetServerStatus();
            string dashboardUrl = _apiEnvironment.GetDashboardUrl();
            string requestId = EdgegapServerDataManager._serverData.RequestId;
            string deploymentDashboardUrl = "";

            if (!string.IsNullOrEmpty(requestId) && !string.IsNullOrEmpty(dashboardUrl))
            {
                deploymentDashboardUrl = $"{dashboardUrl}/arbitrium/deployment/read/{requestId}/";
            }

            VisualElement container = new VisualElement();
            container.AddToClassList("container");

            container.Add(GetHeader("Server Status"));

            VisualElement row = new VisualElement();
            row.AddToClassList("row__status");

            // Status pill
            Label statusLabel = new Label(serverStatus.GetLabelText());
            statusLabel.AddToClassList(serverStatus.GetStatusBgClass());
            statusLabel.AddToClassList("label__status");
            row.Add(statusLabel);

            // Link to dashboard
            if (!string.IsNullOrEmpty(deploymentDashboardUrl))
            {
                row.Add(GetLinkButton("See in the dashboard", deploymentDashboardUrl));
            }
            else
            {
                row.Add(new Label("Could not resolve link to this deployment"));
            }

            container.Add(row);

            return container;
        }

        VisualElement GetDnsSection()
        {
            string serverDns = EdgegapServerDataManager._serverData.Fqdn;

            VisualElement container = new VisualElement();
            container.AddToClassList("container");

            container.Add(GetHeader("Server DNS"));

            VisualElement row = new VisualElement();
            row.AddToClassList("row__dns");
            row.AddToClassList("focusable");

            row.Add(new Label(serverDns));
            row.Add(GetCopyButton("Copy", serverDns));

            container.Add(row);

            return container;
        }

        VisualElement GetPortsSection()
        {
            List<PortMapping> serverPorts = EdgegapServerDataManager._serverData.Ports.Values.ToList();

            VisualElement container = new VisualElement();
            container.AddToClassList("container");

            container.Add(GetHeader("Server Ports"));
            container.Add(GetHeaderRow());

            VisualElement portList = new VisualElement();

            if (serverPorts.Count > 0)
            {
                foreach (PortMapping port in serverPorts)
                {
                    portList.Add(GetRowFromPortResponse(port));
                }
            }
            else
            {
                portList.Add(new Label("No port configured for this app version."));
            }

            container.Add(portList);

            return container;
        }

        public VisualElement GetServerDataVisualTree()
        {
            VisualElement serverDataTree = new VisualElement();
            serverDataTree.styleSheets.Add(_serverDataStylesheet);

            bool hasServerData = EdgegapServerDataManager._serverData != null;
            bool isReady = hasServerData && EdgegapServerDataManager. _serverData.GetServerStatus().IsOneOf(ServerStatus.Ready, ServerStatus.Error);

            if (hasServerData)
            {
                serverDataTree.Add(GetStatusSection());

                if (isReady)
                {
                    serverDataTree.Add(GetDnsSection());
                    serverDataTree.Add(GetPortsSection());
                }
                else
                {
                    serverDataTree.Add(GetInfoText("Additional information will be displayed when the server is ready."));
                }
            }
            else
            {
                serverDataTree.Add(GetInfoText("Server data will be displayed here when a server is running."));
            }

            return serverDataTree;
        }

        void RefreshServerDataContainers()
        {
            foreach (VisualElement serverDataContainer in _serverDataContainers)
            {
                serverDataContainer.Clear();
                serverDataContainer.Add(GetServerDataVisualTree()); // Cannot reuse a same instance of VisualElement
            }
        }

        // save & load /////////////////////////////////////////////////////////
        /// <summary>
        /// Save the tool's serializable data to the EditorPrefs to allow persistence across restarts.
        /// Any field with [SerializeField] will be saved.
        /// </summary>
        void SaveToolData()
        {
            string data = JsonUtility.ToJson(this, false);
            EditorPrefs.SetString(EditorDataSerializationName, data);
        }

        /// <summary>
        /// Load the tool's serializable data from the EditorPrefs to the object, restoring the tool's state.
        /// </summary>
        void LoadToolData()
        {
            string data = EditorPrefs.GetString(EditorDataSerializationName, JsonUtility.ToJson(this, false));
            JsonUtility.FromJsonOverwrite(data, this);
        }
    }
}
*/
