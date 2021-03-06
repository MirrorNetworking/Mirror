using UnityEditor;
using UnityEngine.UIElements;

namespace AssetStoreTools.Utility
{
    internal static class StyleSelector
    {
        private static StyleSheet GetStylesheet(string stylesheetPath)
        {
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(stylesheetPath);
        }

        public static class UploaderWindow
        {
            private const string StylesPath = "Packages/com.unity.asset-store-tools/Editor/Uploader/Styles";

            public static StyleSheet BaseWindowStyle => GetStylesheet($"{StylesPath}/Base/BaseWindow_Main.uss");
            public static StyleSheet BaseWindowTheme => !EditorGUIUtility.isProSkin ?
                GetStylesheet($"{StylesPath}/Base/BaseWindow_Light.uss") :
                GetStylesheet($"{StylesPath}/Base/BaseWindow_Dark.uss");

            public static StyleSheet LoginWindowStyle => GetStylesheet($"{StylesPath}/Login/Login_Main.uss");
            public static StyleSheet LoginWindowTheme => !EditorGUIUtility.isProSkin ?
                GetStylesheet($"{StylesPath}/Login/Login_Light.uss") :
                GetStylesheet($"{StylesPath}/Login/Login_Dark.uss");

            public static StyleSheet UploadWindowStyle => GetStylesheet($"{StylesPath}/Upload/UploadWindow_Main.uss");
            public static StyleSheet UploadWindowTheme => !EditorGUIUtility.isProSkin ?
                GetStylesheet($"{StylesPath}/Upload/UploadWindow_Light.uss") :
                GetStylesheet($"{StylesPath}/Upload/UploadWindow_Dark.uss");

            public static StyleSheet AllPackagesStyle => GetStylesheet($"{StylesPath}/Upload/AllPackages/AllPackages_Main.uss");
            public static StyleSheet AllPackagesTheme => !EditorGUIUtility.isProSkin ?
                GetStylesheet($"{StylesPath}/Upload/AllPackages/AllPackages_Light.uss") :
                GetStylesheet($"{StylesPath}/Upload/AllPackages/AllPackages_Dark.uss");
        }

        public static class ValidatorWindow 
        {
            private const string StylesPath = "Packages/com.unity.asset-store-tools/Editor/Validator/Styles";

            public static StyleSheet BaseWindowStyle => GetStylesheet($"{StylesPath}/Validator_Main.uss");
            public static StyleSheet BaseWindowTheme => !EditorGUIUtility.isProSkin ?
                GetStylesheet($"{StylesPath}/Validator_Light.uss") :
                GetStylesheet($"{StylesPath}/Validator_Dark.uss");
        }
    }
}