using AssetStoreTools.Utility;
using AssetStoreTools.Validator.UIElements;
using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Validator
{
    internal class AssetStoreValidator : AssetStoreToolsWindow
    {
        protected override string WindowTitle => "Asset Store Validator";

        public static Action OnWindowDestroyed;
        
        private AutomatedTestsGroup _automatedTestsGroup;

        protected override void Init()
        {
            minSize = new Vector2(350, 350);

            base.Init();
            this.SetAntiAliasing(4);

            VisualElement root = rootVisualElement;
            
            root.AddToClassList("root");

            // Clean it out, in case the window gets initialized again
            root.Clear();

            // Getting a reference to the USS Document and adding stylesheet to the root
            root.styleSheets.Add(StyleSelector.ValidatorWindow.BaseWindowStyle);
            root.styleSheets.Add(StyleSelector.ValidatorWindow.BaseWindowTheme);

            ConstructWindow();
        }

        private void ConstructWindow()
        {
            _automatedTestsGroup = new AutomatedTestsGroup();
            rootVisualElement.Add(_automatedTestsGroup);
        }

        private void OnDestroy()
        {
            OnWindowDestroyed?.Invoke();
        }
    }
}