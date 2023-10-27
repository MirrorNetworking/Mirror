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
using System.Threading.Tasks;
using Edgegap;
using IO.Swagger.Model;

public class EdgegapWindow : EditorWindow
{
    static readonly HttpClient _httpClient = new HttpClient();

    private const string EditorDataSerializationName = "EdgegapSerializationData";
    private const int ServerStatusCronjobIntervalMs = 10000; // Interval at which the server status is updated

    private readonly System.Timers.Timer _updateServerStatusCronjob = new System.Timers.Timer(ServerStatusCronjobIntervalMs);

    [SerializeField] private string _userExternalIp;
    [SerializeField] private string _apiKey;
    [SerializeField] private ApiEnvironment _apiEnvironment;
    [SerializeField] private string _appName;
    [SerializeField] private string _appVersionName;
    [SerializeField] private string _deploymentRequestId;

    [SerializeField] private string _containerRegistry;
    [SerializeField] private string _containerImageRepo;
    [SerializeField] private string _containerImageTag;
    [SerializeField] private bool _autoIncrementTag = true;


    private VisualTreeAsset _visualTree;
    private bool _shouldUpdateServerStatus = false;

    // Interactable elements
    private EnumField _apiEnvironmentSelect;
    private TextField _apiKeyInput;
    private TextField _appNameInput;
    private TextField _appVersionNameInput;
    private TextField _containerRegistryInput;
    private TextField _containerImageRepoInput;
    private TextField _containerImageTagInput;
    private Toggle _autoIncrementTagInput;
    private Button _connectionButton;
    private Button _serverActionButton;
    private Button _documentationBtn;
    private Button _buildAndPushServerBtn;

    // Readonly elements
    private Label _connectionStatusLabel;
    private VisualElement _serverDataContainer;

    [MenuItem("Edgegap/Server Management")]
    public static void ShowEdgegapToolWindow()
    {
        EdgegapWindow window = GetWindow<EdgegapWindow>();
        window.titleContent = new GUIContent("Edgegap Server Management");
    }

    protected void OnEnable()
    {
        // Set root VisualElement and style
        _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Edgegap/Editor/EdgegapWindow.uxml");
        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Edgegap/Editor/EdgegapWindow.uss");
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
        EdgegapServerDataManager.DeregisterServerDataContainer(_serverDataContainer);
    }

    /// <summary>
    /// Binds the form inputs to the associated variables and initializes the inputs as required.
    /// Requires the VisualElements to be loaded before this call. Otherwise, the elements cannot be found.
    /// </summary>
    private void InitUIElements()
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
        VisualElement serverDataElement = EdgegapServerDataManager.GetServerDataVisualTree();
        EdgegapServerDataManager.RegisterServerDataContainer(serverDataElement);
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
    private string GetExternalIpAddress()
    {
        string externalIpString = new WebClient()
            .DownloadString("http://icanhazip.com")
            .Replace("\\r\\n", "")
            .Replace("\\n", "")
            .Trim();
        var externalIp = IPAddress.Parse(externalIpString);

        return externalIp.ToString();
    }

    private void OpenDocumentationCallback()
    {
        ApiEnvironment selectedApiEnvironment = (ApiEnvironment)_apiEnvironmentSelect.value;
        string documentationUrl = selectedApiEnvironment.GetDocumentationUrl();

        if (!string.IsNullOrEmpty(documentationUrl))
        {
            UnityEngine.Application.OpenURL(documentationUrl);
        }
        else
        {
            string apiEnvName = Enum.GetName(typeof(ApiEnvironment), selectedApiEnvironment);
            Debug.LogWarning($"Could not open documentation for api environment {apiEnvName}: No documentation URL.");
        }
    }

    private void ConnectCallback()
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

    private async void Connect(
        ApiEnvironment selectedApiEnvironment,
        string selectedAppName,
        string selectedAppVersionName,
        string selectedApiTokenValue
    )
    {
        SetToolUIState(ToolState.Connecting);

        _httpClient.BaseAddress = new Uri(selectedApiEnvironment.GetApiUrl());

        string path = $"/v1/app/{selectedAppName}/version/{selectedAppVersionName}";

        // Headers
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", selectedApiTokenValue);

        // Make HTTP request
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

    private void DisconnectCallback()
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

    private float ProgressCounter = 0;

    private void ShowBuildWorkInProgress(string status)
    {
        EditorUtility.DisplayProgressBar("Build and push progress", status, ProgressCounter++ / 50);
    }

    private async void BuildAndPushServer()
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
            if (!await EdgegapBuildUtils.DockerSetupAndInstalationCheck())
            {
                onError("Docker installation not found. Docker can be downloaded from:\n\nhttps://www.docker.com/");
                return;
            }

            // create server build
            var buildResult = EdgegapBuildUtils.BuildServer();
            if (buildResult.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                onError("Edgegap build failed");
                return;
            }


            var registry = _containerRegistry;
            var imageName = _containerImageRepo;
            var tag = _containerImageTag;

            // increment tag for quicker iteration
            if (_autoIncrementTag)
            {
                tag = EdgegapBuildUtils.IncrementTag(tag);
            }

            // create docker image
            await EdgegapBuildUtils.DockerBuild(registry, imageName, tag, ShowBuildWorkInProgress);

            SetToolUIState(ToolState.Pushing);

            // push docker image
            if (!await EdgegapBuildUtils.DockerPush(registry, imageName, tag, ShowBuildWorkInProgress))
            {
                onError("Unable to push docker image to registry. Make sure you're logged in to " + registry);
                return;
            }

            // update edgegap server settings for new tag
            ShowBuildWorkInProgress("Updating server info on Edgegap");
            await UpdateAppTagOnEdgegap(tag);

            // cleanup
            _containerImageTag = tag;
            SyncFormWithObject();
            EditorUtility.ClearProgressBar();
            SetToolUIState(ToolState.Connected);

            Debug.Log("Server built and pushed successfully");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError(ex);
            onError("Edgegap build and push failed");
        }
    }

    private async Task UpdateAppTagOnEdgegap(string newTag)
    {
        string path = $"/v1/app/{_appName}/version/{_appVersionName}";

        // Setup post data
        var updatePatchData = new AppVersionUpdatePatchData { DockerImage = _containerImageRepo, DockerRegistry = _containerRegistry, DockerTag = newTag };
        var json = JsonConvert.SerializeObject(updatePatchData);
        var patchData = new StringContent(json, Encoding.UTF8, "application/json");

        // Make HTTP request
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), path);
        request.Content = patchData;

        HttpResponseMessage response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Could not update Edgegap server tag. Got {(int)response.StatusCode} with response:\n{content}");
        }
    }

    private async void StartServerCallback()
    {
        SetToolUIState(ToolState.ProcessingDeployment); // Prevents being called multiple times.

        const string path = "/v1/deploy";

        // Setup post data
        var deployPostData = new DeployPostData(_appName, _appVersionName, new List<string> { _userExternalIp });
        var json = JsonConvert.SerializeObject(deployPostData);
        var postData = new StringContent(json, Encoding.UTF8, "application/json");

        // Make HTTP request
        HttpResponseMessage response = await _httpClient.PostAsync(path, postData);
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            // Parse response
            var parsedResponse = JsonConvert.DeserializeObject<Deployment>(content);

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

    private async void StopServerCallback()
    {
        string path = $"/v1/stop/{_deploymentRequestId}";

        // Make HTTP request
        HttpResponseMessage response = await _httpClient.DeleteAsync(path);

        if (response.IsSuccessStatusCode)
        {
            UpdateServerStatus();
            SetToolUIState(ToolState.ProcessingDeployment);
        }
        else
        {
            // Parse response
            var content = await response.Content.ReadAsStringAsync();

            Debug.LogError($"Could not stop Edgegap server. Got {(int)response.StatusCode} with response:\n{content}");
        }
    }

    private void StartServerStatusCronjob()
    {
        _updateServerStatusCronjob.Elapsed += (sourceObject, elaspedEvent) => _shouldUpdateServerStatus = true;
        _updateServerStatusCronjob.AutoReset = true;
        _updateServerStatusCronjob.Start();
    }

    private void StopServerStatusCronjob() => _updateServerStatusCronjob.Stop();

    private async void UpdateServerStatus()
    {
        var serverStatusResponse = await FetchServerStatus();

        ToolState toolState;
        ServerStatus serverStatus = serverStatusResponse.GetServerStatus();

        if (serverStatus == ServerStatus.Terminated)
        {
            EdgegapServerDataManager.SetServerData(null, _apiEnvironment);

            if (_updateServerStatusCronjob.Enabled)
            {
                StopServerStatusCronjob();
            }

            _deploymentRequestId = null;
            toolState = ToolState.Connected;
        }
        else
        {
            EdgegapServerDataManager.SetServerData(serverStatusResponse, _apiEnvironment);

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

    private async Task<Status> FetchServerStatus()
    {
        string path = $"/v1/status/{_deploymentRequestId}";

        // Make HTTP request
        HttpResponseMessage response = await _httpClient.GetAsync(path);

        // Parse response
        var content = await response.Content.ReadAsStringAsync();

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

    private void RestoreActiveDeployment()
    {
        ConnectCallback();

        _shouldUpdateServerStatus = true;
        StartServerStatusCronjob();
    }

    private void SyncObjectWithForm()
    {
        _apiKey = _apiKeyInput.value;
        _apiEnvironment = (ApiEnvironment)_apiEnvironmentSelect.value;
        _appName = _appNameInput.value;
        _appVersionName = _appVersionNameInput.value;

        _containerRegistry = _containerRegistryInput.value;
        _containerImageTag = _containerImageTagInput.value;
        _containerImageRepo = _containerImageRepoInput.value;
        _autoIncrementTag = _autoIncrementTagInput.value;
    }

    private void SyncFormWithObject()
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

    private void SetToolUIState(ToolState toolState)
    {
        SetConnectionInfoUI(toolState);
        SetConnectionButtonUI(toolState);
        SetServerActionUI(toolState);
        SetDockerRepoInfoUI(toolState);
    }

    private void SetDockerRepoInfoUI(ToolState toolState)
    {
        var connected = toolState.CanStartDeployment();
        _containerRegistryInput.SetEnabled(connected);
        _autoIncrementTagInput.SetEnabled(connected);
        _containerImageRepoInput.SetEnabled(connected);
        _containerImageTagInput.SetEnabled(connected);

    }

    private void SetConnectionInfoUI(ToolState toolState)
    {
        bool canEditConnectionInfo = toolState.CanEditConnectionInfo();

        _apiKeyInput.SetEnabled(canEditConnectionInfo);
        _apiEnvironmentSelect.SetEnabled(canEditConnectionInfo);
        _appNameInput.SetEnabled(canEditConnectionInfo);
        _appVersionNameInput.SetEnabled(canEditConnectionInfo);
       
    }

    private void SetConnectionButtonUI(ToolState toolState)
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

    private void SetServerActionUI(ToolState toolState)
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

    /// <summary>
    /// Save the tool's serializable data to the EditorPrefs to allow persistence across restarts.
    /// Any field with [SerializeField] will be saved.
    /// </summary>
    private void SaveToolData()
    {
        var data = JsonUtility.ToJson(this, false);
        EditorPrefs.SetString(EditorDataSerializationName, data);
    }

    /// <summary>
    /// Load the tool's serializable data from the EditorPrefs to the object, restoring the tool's state.
    /// </summary>
    private void LoadToolData()
    {
        var data = EditorPrefs.GetString(EditorDataSerializationName, JsonUtility.ToJson(this, false));
        JsonUtility.FromJsonOverwrite(data, this);
    }
}