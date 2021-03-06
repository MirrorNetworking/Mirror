using AssetStoreTools.Utility;
using AssetStoreTools.Utility.Json;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader.UIElements
{
    internal class LoginWindow : VisualElement
    {
        private readonly string REGISTER_URL = "https://publisher.unity.com/access";
        private readonly string FORGOT_PASSWORD_URL = "https://id.unity.com/password/new";
        
        private Button _cloudLoginButton;
        private Button _credentialsLoginButton;

        private Label _cloudLoginLabel;

        private TextField _emailField;
        private TextField _passwordField;

        private Box _errorBox;
        private Label _errorLabel;

        private double _cloudLoginRefreshTime = 1d;
        private double _lastRefreshTime;

        public new class UxmlFactory : UxmlFactory<LoginWindow> { }

        public LoginWindow()
        {
            styleSheets.Add(StyleSelector.UploaderWindow.LoginWindowStyle);
            styleSheets.Add(StyleSelector.UploaderWindow.LoginWindowTheme);
            ConstructLoginWindow();
            EditorApplication.update += UpdateCloudLoginButton;
        }

        public void SetupLoginElements(Action<JsonValue> onSuccess, Action<ASError> onFail)
        {
            this.SetEnabled(true);
            
            _cloudLoginLabel = _cloudLoginButton.Q<Label>(className: "login-description");
            
            _cloudLoginLabel.text = "Cloud login unavailable.";
            _cloudLoginButton.SetEnabled(false);

            _cloudLoginButton.clicked += async () =>
            {
                EnableErrorBox(false);
                this.SetEnabled(false);
                var result = await AssetStoreAPI.LoginWithTokenAsync(CloudProjectSettings.accessToken);
                if (result.Success)
                    onSuccess(result.Response);
                else
                    onFail(result.Error);
            };

            // Normal login
            _credentialsLoginButton.clicked += async () =>
            {
                EnableErrorBox(false);
                
                var validatedFields = ValidateLoginFields(_emailField.text, _passwordField.value);
                this.SetEnabled(!validatedFields);

                if (validatedFields)
                {
                    var result = await AssetStoreAPI.LoginWithCredentialsAsync(_emailField.text, _passwordField.text);
                    if (result.Success)
                        onSuccess(result.Response);
                    else
                        onFail(result.Error);
                }
            };
        }

        public void EnableErrorBox(bool enable, string message=null)
        {
            var displayStyle = enable ? DisplayStyle.Flex : DisplayStyle.None;
            _errorBox.style.display = displayStyle;

            if (!String.IsNullOrEmpty(message))
                _errorLabel.text = message;
        }

        public void ClearLoginBoxes()
        {
            _emailField.value = String.Empty;
            _passwordField.value = String.Empty;
        }
        
        private bool ValidateLoginFields(string email, string password)
        {
            if (string.IsNullOrEmpty(email))
            {
                EnableErrorBox(true, "Email field cannot be empty.");
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                EnableErrorBox(true, "Password field cannot be empty.");
                return false;
            }

            return true;
        }
        
        private void ConstructLoginWindow()
        {
            // Asset Store logo
            Image assetStoreLogo = new Image {name = "AssetStoreLogo"};
            assetStoreLogo.AddToClassList("asset-store-logo");
            
            Add(assetStoreLogo);

            // Cloud login
            VisualElement cloudLogin = new VisualElement {name = "CloudLogin"};
            
            _cloudLoginButton = new Button {name = "LoginButtonCloud"};
            _cloudLoginButton.AddToClassList("login-button-cloud");

            Label loginDescription = new Label {text = "Cloud login unavailable"};
            loginDescription.AddToClassList("login-description");
            
            Label orLabel = new Label {text = "or"};
            orLabel.AddToClassList("or-label");

            _cloudLoginButton.Add(loginDescription);
            
            cloudLogin.Add(_cloudLoginButton);
            cloudLogin.Add(orLabel);
            
            Add(cloudLogin);

            _errorBox = new Box() { name = "LoginErrorBox" };
            _errorBox.AddToClassList("login-error-box");

            var errorImage = new Image();
            _errorBox.Add(errorImage);

            _errorLabel = new Label();
            _errorBox.Add(_errorLabel);

            Add(_errorBox);
            EnableErrorBox(false);
            
            // Manual login
            VisualElement manualLoginBox = new VisualElement {name = "ManualLoginBox"};
            manualLoginBox.AddToClassList("manual-login-box");
            
            // Email input box
            VisualElement inputBoxEmail = new VisualElement();
            inputBoxEmail.AddToClassList("input-box-login");

            Label emailTitle = new Label {text = "Email"};
            _emailField = new TextField();
            
            inputBoxEmail.Add(emailTitle);
            inputBoxEmail.Add(_emailField);
            
            manualLoginBox.Add(inputBoxEmail);

            // Password input box
            VisualElement inputBoxPassword = new VisualElement();
            inputBoxPassword.AddToClassList("input-box-login");

            Label passwordTitle = new Label {text = "Password"};
            _passwordField = new TextField {isPasswordField = true};
            
            inputBoxPassword.Add(passwordTitle);
            inputBoxPassword.Add(_passwordField);
            
            manualLoginBox.Add(inputBoxPassword);

            // Login button
            _credentialsLoginButton = new Button {name = "LoginButtonCredentials"};
            _credentialsLoginButton.AddToClassList("login-button-cred");
            
            Label loginDescriptionCredentials = new Label {text = "Login"};
            loginDescriptionCredentials.AddToClassList("login-description");
            
            _credentialsLoginButton.Add(loginDescriptionCredentials);
            
            manualLoginBox.Add(_credentialsLoginButton);
            
            Add(manualLoginBox);
            
            // Helper buttons
            VisualElement helperBox = new VisualElement {name = "HelperBox"};
            helperBox.AddToClassList("helper-button-box");
            
            Button createAccountButton = new Button {name = "CreateAccountButton", text = "Create Publisher ID"};
            Button forgotPasswordButton = new Button {name = "ForgotPasswordButton", text = "Reset Password"};
            
            createAccountButton.AddToClassList("hyperlink-button");
            forgotPasswordButton.AddToClassList("hyperlink-button");

            createAccountButton.clicked += () => Application.OpenURL(REGISTER_URL);
            forgotPasswordButton.clicked += () => Application.OpenURL(FORGOT_PASSWORD_URL);
            
            helperBox.Add(createAccountButton);
            helperBox.Add(forgotPasswordButton);

            Add(helperBox);
        }

        private void UpdateCloudLoginButton()
        {
            if (_cloudLoginLabel == null)
                return;

            if (_lastRefreshTime + _cloudLoginRefreshTime > EditorApplication.timeSinceStartup)
                return;

            _lastRefreshTime = EditorApplication.timeSinceStartup;
            
            // Cloud login
            if (AssetStoreAPI.IsCloudUserAvailable)
            {
                _cloudLoginLabel.text = $"Login with {CloudProjectSettings.userName}";
                _cloudLoginButton.SetEnabled(true);
            }
            else
            {
                _cloudLoginLabel.text = "Cloud login unavailable";
                _cloudLoginButton.SetEnabled(false);
            }
        }
        
    }
}