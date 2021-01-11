using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [InitializeOnLoad]
    public class MirrorWelcomeWindow : EditorWindow
    {
        #region Sizes and Positions

        //window size of the welcome screen
        private static Vector2 windowSize = new Vector2(500, 600);

        //size and position of the mirror icon
        private static Vector2 iconSize = new Vector2(64, 64);
        private static Vector2 iconPosition = new Vector2((windowSize.x - iconSize.x) / 2, 0);

        //size of the banner
        private static Vector2 bannerSize = new Vector2(windowSize.x, iconSize.y);

        //position and size of the welcome text
        private static Vector2 welcomeTextSize = new Vector2(windowSize.x, 30);
        private static Vector2 welcomeTextPosition = new Vector2(0, -3 + iconSize.y + welcomeTextSize.y / 2);

        //padding and height of the columns
        private static int edgePadding = 2;
        private static int height = 114;

        //position and size of the left column 
        private static Vector2 leftColumnPosition = new Vector2(edgePadding, height + 4);
        private static Vector2 leftColumnSize = new Vector2((windowSize.x - edgePadding) / 3, windowSize.y - height);

        //right column position and size
        private static Vector2 rightColumnPosition = new Vector2((windowSize.x - edgePadding) / 3 + edgePadding * 2, height + 4);
        private static Vector2 rightColumnSize = new Vector2(2 * (windowSize.x - edgePadding) / 3 - edgePadding * 2, windowSize.y - height);

        //minimum button height (in case flexible space is used in the future)
        private static int minButtonHeight = 50;

        //redirect button position and size
        private static int redirectButtonHeight = 30;
        private static Vector2 redirectButtonPosition = new Vector2(edgePadding * 2, windowSize.y - height - redirectButtonHeight - edgePadding * 2);
        private static Vector2 redirectButtonSize = new Vector2(2 * (windowSize.x - edgePadding) / 3 - edgePadding * 6, redirectButtonHeight);

        #endregion

        #region Page variables

        //type of page
        private enum Screens { welcome, changelog, quickstart, bestpractices, templates, faq, sponsor, discord }

        //the current page
        private static Screens currentScreen = Screens.welcome;

        //data type that we want to retrieve when we are using this enum
        private enum PageDataType { header, description, redirectButtonTitle, redirectButtonUrl }

        //scroll position of the changelog
        private Vector2 scrollPos;

        //headers of the different pages
        private static string welcomePageHeader = "Welcome";
        private static string changelogHeader = "Change Log";
        private static string quickStartHeader = "Quick Start Guide";
        private static string bestPracticesHeader = "Best Practices";
        private static string templatesHeader = "Script Templates";
        private static string faqHeader = "FAQ";
        private static string sponsorHeader = "Sponsor Us";

        //descriptions of the different pages
        private static string welcomePageDescription = "Hello! Thank you for installing Mirror. Please visit all the pages on this window. Clicking the button at the bottom of the pages will redirect you to a webpage. Additionally, there are example projects in the Mirror folder that you can look at. \n\nHave fun using Mirror!";
        private static string changelogDescription = "The Change Log is a list of changes made to Mirror. Sometimes these changes can cause your game to break.";
        private static string quickStartDescription = "The Quick Start Guide is meant for people who just started using Mirror. The Quick Start Guide will help new users learn how to accomplish important tasks. It is highly recommended that you complete the guide.";
        private static string bestPracticesDescription = "This page describes the best practices that you should use during development. Currently a work in progress.";
        private static string templatesDescription = "Script templates make it easier to create derived class scripts that inherit from our base classes. The templates have all the possible overrides made for you and organized with comments describing functionality.";
        private static string faqDescription = "The FAQ page holds commonly asked questions. Currently, the FAQ page contains answers to: \n\n   1. Syncing custom data types \n   2. How to connect \n   3. Host migration \n   4. Server lists and matchmaking";
        private static string sponsorDescription = "Sponsoring will give you access to Mirror PRO which gives you special access to tools and priority support.";

        //titles of the redirect buttons
        private static string welcomePageButtonTitle = "Visit API Reference";
        private static string changelogPageButtonTitle = "Visit Change Log";
        private static string quickStartPageButtonTitle = "Visit Quick Start Guide";
        private static string bestPracticesPageButtonTitle = "Visit Best Practices Page";
        private static string templatesPageButtonTitle = "Download Script Templates";
        private static string faqPageButtonTitle = "Visit FAQ";
        private static string sponsorPageButtonTitle = "Sponsor Us";

        #endregion

        #region Urls

        private static string welcomePageUrl = "https://mirror-networking.com/docs/api/Mirror.html";
        private static string quickStartUrl = "https://mirror-networking.com/docs/Articles/CommunityGuides/MirrorQuickStartGuide/index.html";
        private static string changelogUrl = "https://mirror-networking.com/docs/Articles/General/ChangeLog.html";
        private static string bestPracticesUrl = "https://mirror-networking.com/docs/Articles/Guides/BestPractices.html";
        private static string templatesUrl = "https://mirror-networking.com/docs/Articles/General/ScriptTemplates.html";
        private static string faqUrl = "https://mirror-networking.com/docs/Articles/FAQ.html";
        private static string sponsorUrl = "https://github.com/sponsors/vis2k";
        private static string discordInviteUrl = "https://discord.gg/N9QVxbM";

        #endregion

        #region Style

        GUIStyle versionTextStyle = new GUIStyle();
        GUIStyle bannerStyle = new GUIStyle();
        GUIStyle iconStyle = new GUIStyle();
        GUIStyle titleStyle = new GUIStyle();
        GUIStyle descriptionStyle = new GUIStyle();
        GUIStyle headerStyle = new GUIStyle();

        #endregion

        //returns the path to the Mirror folder (ex. Assets/Mirror)
        private static string mirrorPath
        {
            get
            {
                string path = EditorHelper.FindPath<MirrorWelcomeWindow>();
                return path.Split(new[] { "\\Editor" }, System.StringSplitOptions.None)[0];
            }
        }

        //path to the mirror icon
        private static string mirrorIconPath = string.Empty;

        //key for the first start up on the current mirror version
        private static string firstStartUpKey = string.Empty;

        private static string firstTimeMirrorKey = "MirrorWelcome";

        #region Use methods to prevent constant searching for assets

        //called only once
        private static string GetStartUpKey()
        {
            //if the file doesnt exist, return unknown mirror version
            if (!File.Exists(mirrorPath + "/Version.txt")) { return "MirrorUnknown"; }

            //read the Version.txt file
            StreamReader sr = new StreamReader(mirrorPath + "/Version.txt");
            string version = sr.ReadLine();
            sr.Close();
            return "Mirror" + version;
        }

        private static string GetMirrorIconPath()
        {
            return mirrorPath + "/Icon/MirrorIcon.png";
        }

        #endregion

        //get the icon
        private static Texture2D mirrorIcon
        {
            get
            {
                return (Texture2D)AssetDatabase.LoadAssetAtPath(mirrorIconPath, typeof(Texture2D));
            }
        }

        //create the black background for the banner effect
        private static Texture2D iconBackground
        {
            get
            {
                Color[] pixels = new Color[(int)windowSize.x * 64];

                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color(0, 0, 0, 0.78f);
                }

                Texture2D result = new Texture2D((int)windowSize.x, 64);
                result.SetPixels(pixels);
                result.Apply();
                return result;
            }
        }

        #region Handle visibility

        //constructor (called by InitializeOnLoad)
        static MirrorWelcomeWindow()
        {
            EditorApplication.update += ShowWindowOnFirstStart;
        }

        //decide if we should open the window on recompile
        private static void ShowWindowOnFirstStart()
        {
            //if we haven't seen the welcome page on the current mirror version and its our first time using mirror, show the welcome page
            //if there is no version, skip this
            firstStartUpKey = GetStartUpKey();
             if (EditorPrefs.GetBool(firstTimeMirrorKey, false) == false && EditorPrefs.GetBool(firstStartUpKey, false) == false && firstStartUpKey != "MirrorUnknown")
            {
                changelogHeader = "Change Log";
                OpenWindow();
                //now that we have seen the welcome window, set this to true so we don't load the window every time we recompile
                EditorPrefs.SetBool(firstStartUpKey, true);
                EditorPrefs.SetBool(firstTimeMirrorKey, true);
            }
            else if (EditorPrefs.GetBool(firstTimeMirrorKey, false) == true && EditorPrefs.GetBool(firstStartUpKey, false) == false && firstStartUpKey != "MirrorUnknown")
            {
                /**
                 * set the current screen to the changelog rather than the welcome screen because the user has:
                 * 1. Not used mirror in the current version
                 * 2. Has a version.txt file present
                 * 3. Used mirror before
                 */
                currentScreen = Screens.changelog;
                changelogHeader = "Change Log (updated)";
                OpenWindow();
                //now that we have seen the welcome window, set this to true so we don't load the window every time we recompile
                EditorPrefs.SetBool(firstStartUpKey, true);
                EditorPrefs.SetBool(firstTimeMirrorKey, true);
            }

            EditorApplication.update -= ShowWindowOnFirstStart;
        }

        //open the window (also openable through the path below)
        [MenuItem("Mirror/Welcome")]
        public static void OpenWindow()
        {
            mirrorIconPath = GetMirrorIconPath();

            //create the window
            MirrorWelcomeWindow window = GetWindow<MirrorWelcomeWindow>("Mirror Welcome Page");
            //set the position and size
            window.position = new Rect(new Vector2(100, 100), windowSize);
            //set min and max sizes so we cant readjust window size
            window.maxSize = windowSize;
            window.minSize = windowSize;
        }

        #endregion

        private void OnEnable()
        {
            #region Styles

            //styles for the various elements on the window (named accordingly)

            versionTextStyle = new GUIStyle
            {
                normal = new GUIStyleState
                {
                    textColor = Color.white
                },
                fontSize = 10,
                padding = new RectOffset(0, 5, 5, 0),
                alignment = TextAnchor.UpperRight,
            };

            bannerStyle = new GUIStyle
            {
                normal = new GUIStyleState
                {
                    background = iconBackground,
                },
                alignment = TextAnchor.MiddleCenter,
            };

            iconStyle = new GUIStyle
            {
                fixedHeight = 64,
                fixedWidth = 64,
                alignment = TextAnchor.MiddleCenter,
            };

            titleStyle = new GUIStyle
            {
                normal = new GUIStyleState
                {
                    textColor = Color.white
                },
                fontSize = 30,
                alignment = TextAnchor.MiddleCenter,
            };

            descriptionStyle = new GUIStyle
            {
                normal = new GUIStyleState
                {
                    textColor = Color.white
                },
                fontSize = 14,
                padding = new RectOffset(5, 5, 5, 5),
                wordWrap = true,
            };

            headerStyle = new GUIStyle
            {
                normal = new GUIStyleState
                {
                    textColor = Color.white
                },
                fontStyle = FontStyle.Bold,
                fontSize = 20,
                padding = new RectOffset(5, 5, 5, 10),
                wordWrap = true,
            };

            #endregion
        }

        //display the welcome menu
        private void OnGUI()
        {
            DrawBannerAndLogo();

            //draw the version number
            GUI.Label(new Rect(new Vector2(0, 0), new Vector2(windowSize.x, 10)), firstStartUpKey != "MirrorUnknown" ? "v" + firstStartUpKey.Substring(6) : firstStartUpKey.Substring(6), versionTextStyle);

            //draw the welcome text
            GUI.Label(new Rect(welcomeTextPosition, welcomeTextSize), "Welcome to Mirror!", titleStyle);

            //start content box
            GUILayout.BeginHorizontal();

            DrawPageButtons();

            DrawPageDescriptions();

            GUILayout.EndHorizontal();
        }

        #region Drawing Methods

        private void DrawBannerAndLogo()
        {
            //create banner
            using (new GUILayout.AreaScope(new Rect(new Vector2(0, 0), bannerSize), GUIContent.none, bannerStyle))
            {
                //create logo
                GUI.Label(new Rect(iconPosition, iconSize), mirrorIcon, iconStyle);
            }
        }

        private void DrawPageButtons()
        {
            //draw the left column
            using (new GUILayout.AreaScope(new Rect(leftColumnPosition, leftColumnSize), GUIContent.none, EditorStyles.helpBox))
            {
                //column layout
                GUILayout.BeginVertical();
                //buttons
                //if you add additional buttons here, make sure to update the page variables and GetPageData()
                CheckPageButtonClicked(GUILayout.Button("Welcome", GUILayout.MinHeight(minButtonHeight)), Screens.welcome);
                CheckPageButtonClicked(GUILayout.Button("Change Log", GUILayout.MinHeight(minButtonHeight)), Screens.changelog);
                CheckPageButtonClicked(GUILayout.Button("Quick Start Guide", GUILayout.MinHeight(minButtonHeight)), Screens.quickstart);
                CheckPageButtonClicked(GUILayout.Button("Best Practices", GUILayout.MinHeight(minButtonHeight)), Screens.bestpractices);
                CheckPageButtonClicked(GUILayout.Button("Script Templates", GUILayout.MinHeight(minButtonHeight)), Screens.templates);
                CheckPageButtonClicked(GUILayout.Button("FAQ", GUILayout.MinHeight(minButtonHeight)), Screens.faq);
                CheckPageButtonClicked(GUILayout.Button("Sponsor Us", GUILayout.MinHeight(minButtonHeight)), Screens.sponsor);
                CheckPageButtonClicked(GUILayout.Button("Discord", GUILayout.MinHeight(minButtonHeight)), Screens.discord);

                GUILayout.EndVertical();
            }
        }

        private void DrawPageDescriptions()
        {
            //draw the right column
            using (new GUILayout.AreaScope(new Rect(rightColumnPosition, rightColumnSize), GUIContent.none, EditorStyles.helpBox))
            {
                //old code for the changelog that parsed the website changelog
                //if (currentScreen == EScreens.changelog) { scrollPos = GUILayout.BeginScrollView(scrollPos, false, true); }
                //else { GUILayout.BeginVertical(); }
                GUILayout.BeginVertical();

                //draw the header text
                GUILayout.Label(GetPageData(PageDataType.header).ToString(), headerStyle);
                //draw the description text
                GUILayout.Label(GetPageData(PageDataType.description).ToString(), descriptionStyle);
                //draw redirect button
                //if (currentScreen != EScreens.changelog) { CheckRedirectButtonClicked(GUI.Button(new Rect(new Vector2(4, windowSize.y - 114 - 30 - 4), new Vector2(2 * (windowSize.x - 2) / 3 - 12, 30)), GetPageData(EPageDataType.redirectButtonTitle).ToString()), GetPageData(EPageDataType.redirectButtonUrl).ToString()); }
                CheckRedirectButtonClicked(GUI.Button(new Rect(redirectButtonPosition, redirectButtonSize), GetPageData(PageDataType.redirectButtonTitle).ToString()), GetPageData(PageDataType.redirectButtonUrl).ToString());

                //if (currentScreen == EScreens.changelog) {GUILayout.EndScrollView(); }
                //else { GUILayout.EndVertical(); }
                GUILayout.EndVertical();
            }
            //GUILayout.BeginArea(new Rect(rightColumnPosition, rightColumnSize), EditorStyles.helpBox);

            //GUILayout.EndArea();
        }

        #endregion

        #region Functionality Methods

        //ran when the button has been clicked
        private void CheckPageButtonClicked(bool button, Screens newScreen)
        {
            //if the newScreen isn't discord then get the description for the newScreen
            if (button && newScreen != Screens.discord)
            {
                currentScreen = newScreen;
            }
            //otherwise, redirect directly to the discord invite link
            else if (button && newScreen == Screens.discord)
            {
                CheckRedirectButtonClicked(button, discordInviteUrl);
            }
        }

        //redirect to the given url
        private void CheckRedirectButtonClicked(bool button, string url)
        {
            if (button) 
            { 
                Application.OpenURL(url); 
                //reset the change log header when the person has checked the updated changelog
                if(currentScreen == Screens.changelog)
                {
                    changelogHeader = "Change Log";
                }
            }
        }

        //get the page data based on the page and the type needed
        private object GetPageData(PageDataType type)
        {
            string[] returnTypes = new string[8];

            //check the data type, set return types based on data type
            if (type == PageDataType.header)
            {
                returnTypes = new string[] { welcomePageHeader, quickStartHeader, bestPracticesHeader, templatesHeader, faqHeader, sponsorHeader, changelogHeader };
            }
            else if (type == PageDataType.description)
            {
                returnTypes = new string[] { welcomePageDescription, quickStartDescription, bestPracticesDescription, templatesDescription, faqDescription, sponsorDescription, changelogDescription };
            }
            else if (type == PageDataType.redirectButtonTitle)
            {
                returnTypes = new string[] { welcomePageButtonTitle, quickStartPageButtonTitle, bestPracticesPageButtonTitle, templatesPageButtonTitle, faqPageButtonTitle, sponsorPageButtonTitle, changelogPageButtonTitle };
            }
            else if (type == PageDataType.redirectButtonUrl)
            {
                returnTypes = new string[] { welcomePageUrl, quickStartUrl, bestPracticesUrl, templatesUrl, faqUrl, sponsorUrl, changelogUrl };
            }

            //return results based on the current page
            if (currentScreen == Screens.welcome) { return returnTypes[0]; }
            else if (currentScreen == Screens.quickstart) { return returnTypes[1]; }
            else if (currentScreen == Screens.bestpractices) { return returnTypes[2]; }
            else if (currentScreen == Screens.templates) { return returnTypes[3]; }
            else if (currentScreen == Screens.faq) { return returnTypes[4]; }
            else if (currentScreen == Screens.sponsor) { return returnTypes[5]; }
            else if (currentScreen == Screens.changelog) { return returnTypes[6]; }

            return "You forgot to update GetPageData()";
        }

        #endregion
    }
}