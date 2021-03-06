using AssetStoreTools.Utility.Json;
using System;
using System.Collections.Generic;
using AssetStoreTools.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using AssetStoreTools.Uploader.Utility;
using AssetStoreTools.Uploader.UIElements;

namespace AssetStoreTools.Uploader
{
    internal class AssetStoreUploader : AssetStoreToolsWindow
    {
        public const string MinRequiredPackageVersion = "2021.3";

        private const string MainWindowVisualTree = "Packages/com.unity.asset-store-tools/Editor/Uploader/Styles/Base/BaseWindow_Main";
        private const string DebugPhrase = "debug";

        // UI Windows
        private LoginWindow _loginWindow;
        private UploadWindow _uploadWindow;

        private readonly List<char> _debugBuffer = new List<char>();

        public static bool ShowPackageVersionDialog
        {
            get => string.Compare(Application.unityVersion, MinRequiredPackageVersion, StringComparison.Ordinal) == -1 && ASToolsPreferences.Instance.UploadVersionCheck;
            set { ASToolsPreferences.Instance.UploadVersionCheck = value;  ASToolsPreferences.Instance.Save(); }
        }

        protected override string WindowTitle => "Asset Store Uploader";

        protected override void Init()
        {
            if (_loginWindow != null && _uploadWindow != null)
                return;
            
            minSize = new Vector2(400, 430);
            this.SetAntiAliasing(4);

            base.Init();
            
            VisualElement root = rootVisualElement;
            root.AddToClassList("root");

            // Getting a reference to the UXML Document and adding to the root
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{MainWindowVisualTree}.uxml");
            VisualElement uxmlRoot = visualTree.CloneTree();
            uxmlRoot.style.flexGrow = 1;
            root.Add(uxmlRoot);

            root.styleSheets.Add(StyleSelector.UploaderWindow.BaseWindowStyle);
            root.styleSheets.Add(StyleSelector.UploaderWindow.BaseWindowTheme);
            

            // Find necessary windows / views and sets up appropriate functionality
            SetupCoreElements();

            if (!AssetStoreAPI.IsUploading)
            {
                // Should only authenticate if the session is available. Other authentications are only available
                // in the login window. See "SetupLoginElements".
                HideElement(_uploadWindow);
                Authenticate();
            }
            else
            {
                ShowUploadWindow();
            }
        }

        private void OnGUI()
        {
            CheckForDebugMode();
        }

        private void OnDestroy()
        {
            if (AssetStoreAPI.IsUploading)
                EditorUtility.DisplayDialog("Notice", "Assets are still being uploaded to the Asset Store. " +
                    "If you wish to check on the progress, please re-open the Asset Store Uploader window", "OK");
        }

        private void SetupCoreElements()
        {
            _loginWindow = rootVisualElement.Q<LoginWindow>("LoginWindow");
            _uploadWindow = rootVisualElement.Q<UploadWindow>("UploadWindow");

            _loginWindow.SetupLoginElements(OnLoginSuccess, OnLoginFail);
            _uploadWindow.SetupWindows(OnLogout, OnPackageDownloadFail);
        }

        #region Login Interface

        private async void Authenticate()
        {
            ShowLoginWindow();

            // 1 - Check if there's an active session
            // 2 - Check if there's a saved session
            // 3 - Attempt to login via Cloud session token
            // 4 - Prompt manual login
            EnableLoginWindow(false);
            var result = await AssetStoreAPI.LoginWithSessionAsync();
            if (result.Success)
                OnLoginSuccess(result.Response);
            else if (result.SilentFail)
                OnLoginFailSession();
            else
                OnLoginFail(result.Error);
        }
        
        private void OnLoginFail(ASError error)
        {
            Debug.LogError(error.Message);
            
            _loginWindow.EnableErrorBox(true, error.Message);
            EnableLoginWindow(true);
        }

        private void OnLoginFailSession()
        {
            // All previous login methods are unavailable
            EnableLoginWindow(true);
        }

        private void OnLoginSuccess(JsonValue json)
        {
            ASDebug.Log($"Login json\n{json}");

            if (!AssetStoreAPI.IsPublisherValid(json, out var error))
            {
                EnableLoginWindow(true);
                _loginWindow.EnableErrorBox(true, error.Message);
                ASDebug.Log($"Publisher {json["name"]} is invalid.");
                return;
            }
            
            ASDebug.Log($"Publisher {json["name"]} is valid.");
            AssetStoreAPI.SavedSessionId = json["xunitysession"].AsString();
            AssetStoreAPI.LastLoggedInUser = json["username"].AsString();
            
            ShowUploadWindow();
        }

        private void OnPackageDownloadFail(ASError error)
        {
            _loginWindow.EnableErrorBox(true, error.Message);
            EnableLoginWindow(true);
            ShowLoginWindow();
        }

        private void OnLogout()
        {
            AssetStoreAPI.SavedSessionId = String.Empty;
            AssetStoreCache.ClearTempCache();

            _loginWindow.ClearLoginBoxes();
            ShowLoginWindow();
            EnableLoginWindow(true);
        }

        #endregion
        
        #region UI Window Utils
        private void ShowLoginWindow()
        {
            HideElement(_uploadWindow);
            ShowElement(_loginWindow);
        }

        private void ShowUploadWindow()
        {
            HideElement(_loginWindow);
            ShowElement(_uploadWindow);

            _uploadWindow.ShowAllPackagesView();
            _uploadWindow.ShowPublisherEmail(AssetStoreAPI.LastLoggedInUser);
            _uploadWindow.LoadPackages(true, OnPackageDownloadFail);
        }
        
        private void ShowElement(params VisualElement[] elements)
        {
            foreach(var e in elements)
                e.style.display = DisplayStyle.Flex;
        }

        private void HideElement(params VisualElement[] elements)
        {
            foreach(var e in elements)
                e.style.display = DisplayStyle.None;
        }
        
        private void EnableLoginWindow(bool enable)
        {
            _loginWindow.SetEnabled(enable);
        }

        #endregion

        #region Debug Utility
        
        private void CheckForDebugMode()
        {
            Event e = Event.current;
            
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) 
                return;
            
            _debugBuffer.Add(e.keyCode.ToString().ToLower()[0]);
            if (_debugBuffer.Count > DebugPhrase.Length)
                _debugBuffer.RemoveAt(0);

            if (string.Join(string.Empty, _debugBuffer.ToArray()) != DebugPhrase) 
                return;
                
            ASDebug.DebugModeEnabled = !ASDebug.DebugModeEnabled;
            ASDebug.Log($"DEBUG MODE ENABLED: {ASDebug.DebugModeEnabled}");
            _debugBuffer.Clear();
        }
        
        #endregion
    }
}