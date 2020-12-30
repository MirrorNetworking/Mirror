using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/**
 * Docs used:
 * UXML: https://docs.unity3d.com/Manual/UIE-UXML.html
 * USS: https://docs.unity3d.com/Manual/UIE-USS-SupportedProperties.html
 * Unity Guide: https://learn.unity.com/tutorial/uielements-first-steps/?tab=overview#5cd19ef9edbc2a1156524842
 * External Guide: https://www.raywenderlich.com/6452218-uielements-tutorial-for-unity-getting-started#toc-anchor-010
 */

namespace Mirror
{

    //this script handles the functionality of the UI

    [InitializeOnLoad]
    public class WelcomeWindow : EditorWindow
    {
        #region Setup

        #region Urls

        private const string WelcomePageUrl = "https://mirrorng.github.io/MirrorNG/";
        private const string QuickStartUrl = "https://mirrorng.github.io/MirrorNG/Articles/Guides/CommunityGuides/MirrorQuickStartGuide/index.html";
        private const string ChangelogUrl = "https://github.com/MirrorNG/MirrorNG/commits/master";
        private const string BestPracticesUrl = "https://mirrorng.github.io/MirrorNG/Articles/Guides/BestPractices.html";
        private const string FaqUrl = "https://mirrorng.github.io/MirrorNG/Articles/Guides/FAQ.html";
        private const string SponsorUrl = "";
        private const string DiscordInviteUrl = "https://discord.gg/N9QVxbM";

        #endregion

        //window size of the welcome screen
        private static Vector2 windowSize = new Vector2(500, 600);

        //get the start up key
        private static string firstStartUpKey = string.Empty;

        #region version

        //called only once
        private static string GetVersion()
        {
            return typeof(NetworkIdentity).Assembly.GetName().Version.ToString();
        }

        #endregion

        #region Handle visibility

        //constructor (called by InitializeOnLoad)
        static WelcomeWindow()
        {
            EditorApplication.update += ShowWindowOnFirstStart;
        }

        //decide if we should open the window on recompile
        private static void ShowWindowOnFirstStart()
        {
            //if we haven't seen the welcome page on the current mirror version, show it
            //if there is no version, skip this
            firstStartUpKey = GetVersion();
            if (!EditorPrefs.GetBool(firstStartUpKey, false) && firstStartUpKey != "MirrorUnknown")
            {
                OpenWindow();
                //now that we have seen the welcome window, set this this to true so we don't load the window every time we recompile (for the current version)
                EditorPrefs.SetBool(firstStartUpKey, true);
            }

            EditorApplication.update -= ShowWindowOnFirstStart;
        }

        //open the window (also openable through the path below)
        [MenuItem("Window/MirrorNG/Welcome")]
        public static void OpenWindow()
        {
            //create the window
            WelcomeWindow window = GetWindow<WelcomeWindow>("MirrorNG Welcome Page");
            //set the position and size
            window.position = new Rect(new Vector2(100, 100), windowSize);
            //set min and max sizes so we cant readjust window size
            window.maxSize = windowSize;
            window.minSize = windowSize;
        }

        #endregion

        #endregion

        #region Displaying UI

        //the code to handle display and button clicking
        private void OnEnable()
        {
            //Load the UI
            //Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;
            VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("WelcomeWindow");
            StyleSheet uss = Resources.Load<StyleSheet>("WelcomeWindow");

            //Load the descriptions

            uxml.CloneTree(root);
            root.styleSheets.Add(uss);

            //set the version text
            Label versionText = root.Q<Label>("VersionText");
            versionText.text = "v" + GetVersion();

            #region Page buttons

            ConfigureTab("WelcomeButton", "Welcome", WelcomePageUrl);
            ConfigureTab("ChangeLogButton", "ChangeLog", ChangelogUrl);
            ConfigureTab("QuickStartButton", "QuickStart", QuickStartUrl);
            ConfigureTab("BestPracticesButton", "BestPractices", BestPracticesUrl);
            ConfigureTab("FaqButton", "Faq", FaqUrl);
            ConfigureTab("SponsorButton", "Sponsor", SponsorUrl);

            Button DiscordButton = root.Q<Button>("DiscordButton");
            DiscordButton.clicked += () => Application.OpenURL(DiscordInviteUrl);

            ShowTab("Welcome");
            #endregion
        }

        private void ConfigureTab(string tabButtonName, string tab, string url)
        {
            Button tabButton = rootVisualElement.Q<Button>(tabButtonName);
            tabButton.clicked += () => ShowTab(tab);
            Button redirectButton = rootVisualElement.Q<VisualElement>(tab).Q<Button>("Redirect");
            redirectButton.clicked += () => Application.OpenURL(url);
        }

        private void ShowTab(string screen)
        {
            VisualElement rightColumn = rootVisualElement.Q<VisualElement>("RightColumnBox");
            foreach (VisualElement tab in rightColumn.Children())
            {
                tab.style.display = tab.name == screen ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        #endregion
    }

}
