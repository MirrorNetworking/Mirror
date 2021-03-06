using System;
using System.Collections.Generic;
using System.Linq;
using AssetStoreTools.Uploader.Data;
using AssetStoreTools.Uploader.Utility;
using AssetStoreTools.Utility;
using AssetStoreTools.Validator.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader.UIElements
{
    internal class AllPackageView : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<AllPackageView> { }

        private enum PackageSorting
        {
            Name,
            Category,
            Date
        }

        // Package Data
        private readonly string[] _priorityGroups = { "Draft", "Published" };
        private readonly List<PackageGroup> _packageGroups;
        private List<PackageView> _allPackages;

        // Visual Elements
        private readonly ScrollView _packageScrollView;
        private Scroller _packageViewScroller;

        // Sorting data
        private PackageSorting _activeSorting;

        // Spinner
        private VisualElement _spinnerBox;
        private Image _loadingSpinner;
        private int _spinIndex;
        private double _spinTimer;
        private double _spinThreshold = 0.1;

        public Action<bool> RefreshingPackages;

        public AllPackageView()
        {
            _packageScrollView = new ScrollView();
            _allPackages = new List<PackageView>();
            _packageGroups = new List<PackageGroup>();

            _activeSorting = PackageSorting.Name; // Default sorting type returned by the metadata JSON       

            styleSheets.Add(StyleSelector.UploaderWindow.AllPackagesStyle);
            styleSheets.Add(StyleSelector.UploaderWindow.AllPackagesTheme);
            ConstructAllPackageView();

            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            EditorApplication.playModeStateChanged += PlayModeStateChanged;

            ValidationState.Instance.OnJsonSave -= RepaintPackageIcons;
            ValidationState.Instance.OnJsonSave += RepaintPackageIcons;
        }

        #region Element Setup

        private void ConstructAllPackageView()
        {
            SetupFilteringTools();
            SetupSpinner();

            Add(_packageScrollView);
        }

        private void SetupFilteringTools()
        {
            // Top Toolbar
            var topToolsRow = new VisualElement { name = "TopToolsRow" };
            topToolsRow.AddToClassList("top-tools-row");

            // Search
            var searchField = new ToolbarSearchField { name = "SearchField" };
            searchField.AddToClassList("package-search-field");

            // Sorting menu button
            var sortMenu = new ToolbarMenu() { text = "Sort by name, A→Z" };
            sortMenu.menu.AppendAction("Sort by name, A→Z", (_) => { sortMenu.text = "Sort by name, A→Z"; Sort(PackageSorting.Name); });
            sortMenu.menu.AppendAction("Sort by last updated", (_) => { sortMenu.text = "Sort by last updated"; Sort(PackageSorting.Date); });
            sortMenu.menu.AppendAction("Sort by category, A→Z", (_) => { sortMenu.text = "Sort by category, A→Z"; Sort(PackageSorting.Category); });
            sortMenu.AddToClassList("sort-menu");

            // Finalize the bar
            topToolsRow.Add(searchField);
            topToolsRow.Add(sortMenu);
            Add(topToolsRow);
            
            // Add Callbacks and click events
            searchField.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                var searchString = evt.newValue.ToLower();
                SearchFilter(searchString);
            });
        }

        private void SearchFilter(string filter)
        {
            foreach (var g in _packageGroups)
                g.SearchFilter(filter);
        }

        private void SetupReadOnlyInfoBox(string infoText)
        {
            var groupHeader = new Box { name = "GroupReadOnlyInfoBox" };
            groupHeader.AddToClassList("group-info-box");

            var infoImage = new Image();
            groupHeader.Add(infoImage);

            var infoLabel = new Label { text = infoText };
            groupHeader.Add(infoLabel);

            _packageScrollView.Add(groupHeader);
        }

        #endregion

        #region Package Display

        public async void ShowPackagesList(bool useCached, Action<ASError> onFail)
        {
            // Clear existing packages in the UI
            ClearPackages();

            // Enable spinner and disable refreshing
            EnableSpinner();
            RefreshingPackages?.Invoke(true);

            // Read package metadata from the Publisher portal
            PackageFetcher packageFetcher = new PackageFetcher();
            var result = await packageFetcher.Fetch(useCached);

            if (!result.Success)
            {
                if (result.SilentFail)
                    return;

                ASDebug.LogError(result.Error.Message);
                onFail?.Invoke(result.Error);
            }

            var packages = result.Packages;
            var json = result.Json;

            // Clear before appending as well
            ClearPackages();

            if (packages == null)
            {
                RefreshingPackages?.Invoke(false);
                DisplayNoPackages();
                return;
            }

            DisplayAllPackages(packages);

            // Only performed after adding all packages to prevent slowdowns. Sorting also repaints the view
            Sort(_activeSorting);

            RefreshingPackages?.Invoke(false);
            DisableSpinner();

            AssetStoreAPI.GetPackageThumbnails(json, true, (id, texture) =>
            {
                var package = GetPackage(id);
                var packageImage = package.Q<Image>();
                packageImage.style.backgroundImage = texture;

                if (texture == null)
                    packageImage.AddToClassList("package-image-not-found");
            },
            (id, error) =>
            {
                ASDebug.LogWarning($"Package {id} could not download thumbnail successfully\n{error.Exception}");
            });
        }

        public void ClearPackages()
        {
            _allPackages.Clear();
            _packageScrollView.Clear();
            _packageGroups.Clear();
        }

        private void DisplayNoPackages()
        {
            SetupReadOnlyInfoBox("You don't have packages yet. Please visit Publishing Portal if you " +
                "would like to create one.");
            DisableSpinner();
        }

        private void DisplayAllPackages(ICollection<PackageData> packages)
        {
            // Each package has an identifier and a bunch of data (current version id, name, etc.)
            foreach (var package in packages)
            {
                AddPackage(package);
            }
        }

        private void AddPackage(PackageData packageData)
        {
            var newEntry = PackageViewStorer.GetPackage(packageData);
            _allPackages.Add(newEntry);
        }

        private VisualElement GetPackage(string id)
        {
            return _allPackages.FirstOrDefault(package => package.PackageId == id);
        }

        private void Repaint()
        {
            _packageScrollView.Clear();
            _packageGroups.Clear();

            var groupedDict = new SortedDictionary<string, List<PackageView>>();

            // Group packages by status into a dictionary
            foreach (var p in _allPackages)
            {
                var status = char.ToUpper(p.Status.First()) + p.Status.Substring(1);

                if (!groupedDict.ContainsKey(status))
                    groupedDict.Add(status, new List<PackageView>());

                groupedDict[status].Add(p);
            }

            // Add prioritized status groups first
            foreach (var group in _priorityGroups)
            {
                if (!groupedDict.ContainsKey(group))
                    continue;

                AddGroup(group, groupedDict[group], true);
                groupedDict.Remove(group);

                // After adding the 'Draft' group, an infobox indicating that other groups are non-interactable is added
                if (group == "Draft" && groupedDict.Count > 0)
                    SetupReadOnlyInfoBox("Only packages with a 'Draft' status can be selected for uploading Assets");
            }

            // Add any leftover status groups
            foreach (var c in groupedDict.Keys)
            {
                AddGroup(c, groupedDict[c], false);
            }

            // Shared group adding method for priority and non-priority groups
            void AddGroup(string groupName, List<PackageView> packages, bool createExpanded)
            {
                var group = new PackageGroup(groupName, createExpanded)
                {
                    OnSliderChange = AdjustVerticalSliderPosition
                };

                foreach (var p in packages)
                    group.AddPackage(p);

                _packageGroups.Add(group);
                _packageScrollView.Add(group);
            }
        }

        private void RepaintPackageIcons()
        {
            foreach (var package in _allPackages)
            {
                if (!AssetStoreCache.GetCachedTexture(package.PackageId, out Texture2D texture)) 
                    continue;
                
                var packageImage = package.Q<Image>();
                packageImage.style.backgroundImage = texture;
            }
        }

        private void AdjustVerticalSliderPosition(float delta)
        {
            if (_packageViewScroller == null)
                _packageViewScroller = this.Q<Scroller>(className: "unity-scroll-view__vertical-scroller");

            _packageViewScroller.value += delta;
        }

        #endregion

        #region Package View Sorting

        private void Sort(PackageSorting sortBy)
        {
            if (sortBy == _activeSorting && _packageScrollView.childCount > 0)
                return;

            switch (sortBy)
            {
                case PackageSorting.Name:
                    SortByName(false);
                    break;
                case PackageSorting.Date:
                    SortByDate(true);
                    break;
                case PackageSorting.Category:
                    SortByCategory(false);
                    break;
            }

            _activeSorting = sortBy;
        }

        private void SortByName(bool descending)
        {
            if (!descending)
                _allPackages = _allPackages.OrderBy(p => p.PackageName).ToList();
            else
                _allPackages = _allPackages.OrderByDescending(p => p.PackageName).ToList();

            Repaint();
        }

        private void SortByCategory(bool descending)
        {
            if (!descending)
                _allPackages = _allPackages.OrderBy(p => p.Category).ThenBy(p => p.PackageName).ToList();
            else
                _allPackages = _allPackages.OrderByDescending(p => p.Category).ThenBy(p => p.PackageName).ToList();

            Repaint();
        }

        private void SortByDate(bool descending)
        {
            if (!descending)
                _allPackages = _allPackages.OrderBy(p => p.LastUpdatedDate).ThenBy(p => p.PackageName).ToList();
            else
                _allPackages = _allPackages.OrderByDescending(p => p.LastUpdatedDate).ThenBy(p => p.PackageName).ToList();

            Repaint();
        }
        
        private void PlayModeStateChanged(PlayModeStateChange playModeState)
        {
            if (playModeState == PlayModeStateChange.EnteredEditMode)
            {
                RepaintPackageIcons();
            }
        }

        #endregion

        #region Spinner

        private void SetupSpinner()
        {
            _spinnerBox = new VisualElement {name = "SpinnerBox"};
            _spinnerBox.AddToClassList("spinner-box");

            _loadingSpinner = new Image {name = "SpinnerImage"};
            _loadingSpinner.AddToClassList("spinner-image");
            
            _spinnerBox.Add(_loadingSpinner);
            Add(_spinnerBox);
        }
        
        private void UpdateSpinner()
        {
            if (_loadingSpinner == null)
                return;
            
            if (_spinTimer + _spinThreshold > EditorApplication.timeSinceStartup) 
                return;
            
            _spinTimer = EditorApplication.timeSinceStartup;
            _loadingSpinner.image = EditorGUIUtility.IconContent($"WaitSpin{_spinIndex:00}").image;
                
            _spinIndex += 1;
                
            if (_spinIndex > 11)
                _spinIndex = 0;
        }

        private void EnableSpinner()
        {
            EditorApplication.update += UpdateSpinner;
            _packageScrollView.style.display = DisplayStyle.None;
            _spinnerBox.style.display = DisplayStyle.Flex;
        }

        private void DisableSpinner()
        {
            EditorApplication.update -= UpdateSpinner;
            _packageScrollView.style.display = DisplayStyle.Flex;
            _spinnerBox.style.display = DisplayStyle.None;
        }

        #endregion
    }
}