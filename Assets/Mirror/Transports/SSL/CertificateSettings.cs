using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [Serializable]
    public class CertificateSettings
    {
        [Tooltip("Path to the certificate file (pass in either a relative path or an absolute path)")]
        public string CertificatePath = "";

        [Tooltip("Is the certificate private key file password protected? (default: false)")]
        public bool PasswordProtected;

        [Tooltip(
            "Path to a file that just contains the password for the certificate file (pass in either a relative path or an absolute path)")]
        public string PasswordFilePath = "";

        public X509Certificate2 Certificate
        {
            get
            {
                if (PasswordProtected)
                {
                    return NewPasswordProtectedCertificate();
                }
                return NewCertificate();
            }
        }

        public string CertPassword()
        {
            return File.ReadAllText(PasswordFilePath);
        }

        private X509Certificate2 NewPasswordProtectedCertificate()
        {
            if (!ValidateCertificatePath(CertificatePath))
            {
                Debug.LogError("Certificate path is invalid (" + CertificatePath + "). Unable to create certificate.");
                return null;
            }
            if (!ValidatePasswordFilePath(PasswordFilePath))
            {
                Debug.LogError("Password file path is invalid (" + PasswordFilePath + "). Unable to create certificate.");
                return null;
            }
            string password = CertPassword();
            return new X509Certificate2(CertificatePath, password);
        }

        private X509Certificate2 NewCertificate()
        {
            if (!ValidateCertificatePath(CertificatePath))
            {
                Debug.LogError("Certificate path is invalid (" + CertificatePath + "). Unable to create certificate.");
                return null;
            }
            return new X509Certificate2(CertificatePath);
        }

        private static bool ValidatePasswordFilePath(string passwordFilePath)
        {
            if (string.IsNullOrEmpty(passwordFilePath))
            {
                Debug.LogError("Password file path is empty (" + passwordFilePath + ")");
                return false;
            }
            string passwordFullPath = Path.GetFullPath(passwordFilePath);
            if (!File.Exists(passwordFilePath))
            {
                Debug.LogError("Password file does not exist (" + passwordFilePath + ")");
                return false;
            }
            return true;
        }

        private static bool ValidateCertificatePath(string certificatePath)
        {
            if (string.IsNullOrEmpty(certificatePath))
            {
                Debug.LogError("Certificate path is empty (" + certificatePath + ")");
                return false;
            }
            string certFullPath = Path.GetFullPath(certificatePath);
            if (!File.Exists(certFullPath))
            {
                Debug.LogError("Certificate file does not exist (" + certFullPath + ")");
                return false;
            }
            string certExtension = Path.GetExtension(certFullPath);
            if (certExtension != ".cer" || certExtension != ".crt" || certExtension != ".pfx")
            {
                Debug.LogError("Certificate file is not a valid certificate file (" + certFullPath + ")");
                return false;
            }
            return true;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(CertificateSettings))]
    public class CertificateSettingsDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            position.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(position, label);

            position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;

            Rect certPathRect = new Rect(position.x, position.y, position.width - 60, EditorGUIUtility.singleLineHeight); // Reduce width for button
            Rect certBrowseRect = new Rect(position.x + position.width - 60, position.y, 60, EditorGUIUtility.singleLineHeight); // Button rect
            Rect certPasswordProtectedRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);

            SerializedProperty certPath = property.FindPropertyRelative(nameof(CertificateSettings.CertificatePath));
            EditorGUI.PropertyField(certPathRect, certPath);

            if (GUI.Button(certBrowseRect, "Browse"))
            {
                string path = EditorUtility.OpenFilePanel("Select Certificate File", "", "pfx,crt,cert,pem");
                if (!string.IsNullOrEmpty(path))
                {
                    certPath.stringValue = path;
                }
            }

            SerializedProperty certPasswordProtected = property.FindPropertyRelative(nameof(CertificateSettings.PasswordProtected));
            EditorGUI.PropertyField(certPasswordProtectedRect, certPasswordProtected);

            if (certPasswordProtected.boolValue)
            {
                position.y += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2;
                Rect passwordFilePathRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                SerializedProperty passwordFilePath = property.FindPropertyRelative(nameof(CertificateSettings.PasswordFilePath));
                EditorGUI.PropertyField(passwordFilePathRect, passwordFilePath);
                Rect passwordBrowseRect = new Rect(position.x + position.width - 60, position.y, 60, EditorGUIUtility.singleLineHeight); // Button rect
                if (GUI.Button(passwordBrowseRect, "Browse"))
                {
                    string path = EditorUtility.OpenFilePanel("Select Password File", "", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        passwordFilePath.stringValue = path;
                    }
                }
            }

            EditorGUI.indentLevel = originalIndent;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            bool certPasswordProtected = property.FindPropertyRelative(nameof(CertificateSettings.PasswordProtected)).boolValue;
            return certPasswordProtected ? (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 4 : (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3;
        }
    }
#endif
}
