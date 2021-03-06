using System;
using AssetStoreTools.Uploader.Utility;
using AssetStoreTools.Utility;
using UnityEditor;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader.UIElements
{
    internal class UploadWindow : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<UploadWindow> { }

        // Views
        private AllPackageView _allPackageView;

        // Toolbar elements
        private Label _accountEmailLabel;
        private Button _refreshButton;
        private Button _logoutButton;

        public UploadWindow()
        {
            styleSheets.Add(StyleSelector.UploaderWindow.UploadWindowStyle);
            styleSheets.Add(StyleSelector.UploaderWindow.UploadWindowTheme);
        }

        public void SetupWindows(Action onLogout, Action<ASError> onPackageDownloadFail)
        {
            _allPackageView = this.Q<AllPackageView>("AllPackageView");            
            SetupBottomToolbar(onLogout, onPackageDownloadFail);
        }

        private void SetupBottomToolbar(Action onLogout, Action<ASError> onPackageDownloadFail)
        {
            // Bottom Tools Row
            VisualElement bottomToolsRow = new VisualElement { name = "BottomToolsRow" };
            bottomToolsRow.AddToClassList("bottom-tools-row");

            // Left side of the toolbar
            VisualElement leftSideContainer = new VisualElement { name = "LeftSideContainer" };
            leftSideContainer.AddToClassList("toolbar-left-side-container");

            _accountEmailLabel = new Label { name = "AccountEmail" };
            _accountEmailLabel.AddToClassList("account-name");

            leftSideContainer.Add(_accountEmailLabel);

            // Right side of the toolbar
            VisualElement rightSideContainer = new VisualElement { name = "RightSideContainer" };
            rightSideContainer.AddToClassList("toolbar-right-side-container");

            // Refresh button
            _refreshButton = new Button { name = "RefreshButton", text = "Refresh" };
            _refreshButton.AddToClassList("refresh-button");
            _refreshButton.clicked += () => _allPackageView.ShowPackagesList(false, onPackageDownloadFail);
            _allPackageView.RefreshingPackages += (isRefreshing) => _refreshButton.SetEnabled(!isRefreshing);

            // Logout button
            _logoutButton = new Button { name = "LogoutButton", text = "Logout" };
            _logoutButton.AddToClassList("logout-button");
            _logoutButton.clicked += () => Logout(onLogout);

            rightSideContainer.Add(_refreshButton);
            rightSideContainer.Add(_logoutButton);

            // Constructing the final toolbar
            bottomToolsRow.Add(leftSideContainer);
            bottomToolsRow.Add(rightSideContainer);

            Add(bottomToolsRow);
        }

        public void LoadPackages(bool useCached, Action<ASError> onPackageDownloadFail)
        {
            _allPackageView.ShowPackagesList(useCached, onPackageDownloadFail);
        }

        public void ShowAllPackagesView()
        {
            _logoutButton.style.display = DisplayStyle.Flex;
            _allPackageView.style.display = DisplayStyle.Flex;
        }

        public void ShowPublisherEmail(string publisherEmail)
        {
            _accountEmailLabel.text = publisherEmail;
        }

        private void Logout(Action onLogout)
        {
            if (AssetStoreAPI.IsUploading && !EditorUtility.DisplayDialog("Notice",
                "Assets are still being uploaded to the Asset Store. Logging out will cancel all uploads in progress.\n\n" +
                "Would you still like to log out?", "Yes", "No"))
                return;

            AssetStoreAPI.AbortDownloadTasks();
            AssetStoreAPI.AbortUploadTasks();
            PackageViewStorer.Reset();

            _allPackageView.ClearPackages();
            onLogout?.Invoke();
        }

    }
}