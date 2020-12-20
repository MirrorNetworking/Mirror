using System.IO;
using UnityEditor;
using UnityEngine;

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
    private enum EScreens { welcome, changelog, quickstart, bestpractices, templates, faq, sponsor, discord }

    //the current page
    private EScreens currentScreen = EScreens.welcome;

    //data type that we want to retrieve when we are using this enum
    private enum EPageDataType { header, description, redirectButtonTitle, redirectButtonUrl }

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
    private static string faqDescription = "The FAQ page holds commonly asked questions. Currently, the FAQ page contains answers to: \n\n   1. Syncing custom data types \n   2. How to connect";
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

    //returns the path to the Mirror folder (ex. Assets/Mirror)
    private static string mirrorPath 
    { 
        get 
        {
            //get an array of results based on the search
            string[] results = AssetDatabase.FindAssets("Version", new string[] { "Assets" });

            //loop through every result
            foreach (string guid in results)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                //if the path contains Mirror/Version.txt, then we have found the Mirror folder
                if (path.Contains("Mirror/Version.txt"))
                {
                    return path.Remove(path.IndexOf("/Version.txt"));
                }
            }

            //return nothing if path wasn't found
            return "";
        } 
    } 

    //path to the mirror icon
    private static string mirrorIconPath = string.Empty;

    //get the start up key
    private static string firstStartUpKey = string.Empty;

    #region Use methods to prevent constant searching for assets

    //called only once
    private static string GetStartUpKey()
    {
        //if the path is empty, return unknown mirror version
        if (mirrorPath == "") { return "MirrorUnknown"; }

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

            for(int i = 0; i < pixels.Length; i++)
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
        //if we haven't seen the welcome page on the current mirror version, show it
        //if there is no version, skip this
        firstStartUpKey = GetStartUpKey();
        if(EditorPrefs.GetBool(firstStartUpKey, false) == false && firstStartUpKey != "MirrorUnknown")
        {
            OpenWindow();
            //now that we have seen the welcome window, set this this to true so we don't load the window every time we recompile (for the current version)
            EditorPrefs.SetBool(firstStartUpKey, true);
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

    //display the welcome menu
    private void OnGUI()
    {
        #region Styles

        //styles for the various elements on the window (named accordingly)

        GUIStyle versionTextStyle = new GUIStyle
        {
            normal = new GUIStyleState
            {
                textColor = Color.white
            },
            fontSize = 10,
            padding = new RectOffset(0, 5, 5, 0),
            alignment = TextAnchor.UpperRight,
        };

        GUIStyle bannerStyle = new GUIStyle
        {
            normal = new GUIStyleState
            {
                background = iconBackground,
            },
        };

        GUIStyle iconStyle = new GUIStyle
        {
            fixedHeight = 64,
            fixedWidth = 64,
            alignment = TextAnchor.MiddleCenter,
        };

        GUIStyle titleStyle = new GUIStyle 
        { 
            normal = new GUIStyleState 
            { 
                textColor = Color.white 
            }, 
            fontSize = 30, 
            alignment = TextAnchor.MiddleCenter,
        };

        GUIStyle descriptionStyle = new GUIStyle
        {
            normal = new GUIStyleState
            {
                textColor = Color.white
            },
            fontSize = 14,
            padding = new RectOffset(5, 5, 5, 5),
            wordWrap = true,
        };

        GUIStyle headerStyle = new GUIStyle
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

        #region Draw the banner and logo

        //create banner
        GUILayout.BeginArea(new Rect(new Vector2(0, 0), bannerSize), bannerStyle);
        //create logo
        GUI.Label(new Rect(iconPosition, iconSize), mirrorIcon, iconStyle);
        GUILayout.EndArea();

        #endregion

        //draw the version number
        GUI.Label(new Rect(new Vector2(0, 0), new Vector2(windowSize.x, 10)), firstStartUpKey != "MirrorUnknown" ? "v" + firstStartUpKey.Substring(6) : firstStartUpKey.Substring(6), versionTextStyle);

        //draw the welcome text
        GUI.Label(new Rect(welcomeTextPosition, welcomeTextSize), "Welcome to Mirror!", titleStyle);

        //start content box
        GUILayout.BeginHorizontal();

        #region Draw page buttons

        //draw the left column
        GUILayout.BeginArea(new Rect(leftColumnPosition, leftColumnSize), EditorStyles.helpBox);
        //column layout
        GUILayout.BeginVertical();
        //buttons
        //if you add additional buttons here, make sure to update the page variables and GetPageData()
        CheckPageButtonClicked(GUILayout.Button("Welcome", GUILayout.MinHeight(minButtonHeight)), EScreens.welcome);
        CheckPageButtonClicked(GUILayout.Button("Change Log", GUILayout.MinHeight(minButtonHeight)), EScreens.changelog);
        CheckPageButtonClicked(GUILayout.Button("Quick Start Guide", GUILayout.MinHeight(minButtonHeight)), EScreens.quickstart);
        CheckPageButtonClicked(GUILayout.Button("Best Practices", GUILayout.MinHeight(minButtonHeight)), EScreens.bestpractices);
        CheckPageButtonClicked(GUILayout.Button("Script Templates", GUILayout.MinHeight(minButtonHeight)), EScreens.templates);
        CheckPageButtonClicked(GUILayout.Button("FAQ", GUILayout.MinHeight(minButtonHeight)), EScreens.faq);
        CheckPageButtonClicked(GUILayout.Button("Sponsor Us", GUILayout.MinHeight(minButtonHeight)), EScreens.sponsor);
        CheckPageButtonClicked(GUILayout.Button("Discord", GUILayout.MinHeight(minButtonHeight)), EScreens.discord);

        GUILayout.EndVertical();
        GUILayout.EndArea();

        #endregion

        #region Draw page description box

        //draw the right column
        GUILayout.BeginArea(new Rect(rightColumnPosition, rightColumnSize), EditorStyles.helpBox);
        //if (currentScreen == EScreens.changelog) { scrollPos = GUILayout.BeginScrollView(scrollPos, false, true); }
        //else { GUILayout.BeginVertical(); }
        GUILayout.BeginVertical();

        //draw the header text
        GUILayout.Label(GetPageData(EPageDataType.header).ToString(), headerStyle);
        //draw the description text
        GUILayout.Label(GetPageData(EPageDataType.description).ToString(), descriptionStyle);
        //draw redirect button
        //if (currentScreen != EScreens.changelog) { CheckRedirectButtonClicked(GUI.Button(new Rect(new Vector2(4, windowSize.y - 114 - 30 - 4), new Vector2(2 * (windowSize.x - 2) / 3 - 12, 30)), GetPageData(EPageDataType.redirectButtonTitle).ToString()), GetPageData(EPageDataType.redirectButtonUrl).ToString()); }
        CheckRedirectButtonClicked(GUI.Button(new Rect(redirectButtonPosition, redirectButtonSize), GetPageData(EPageDataType.redirectButtonTitle).ToString()), GetPageData(EPageDataType.redirectButtonUrl).ToString());

        //if (currentScreen == EScreens.changelog) {GUILayout.EndScrollView(); }
        //else { GUILayout.EndVertical(); }
        GUILayout.EndVertical();
        GUILayout.EndArea();

        #endregion

        GUILayout.EndHorizontal();
    }

    //ran when the button has been clicked
    private void CheckPageButtonClicked(bool button, EScreens newScreen)
    {
        //if the newScreen isn't discord then get the description for the newScreen
        if(button && newScreen != EScreens.discord)
        {
            currentScreen = newScreen;
        }
        //otherwise, redirect directly to the discord invite link
        else if(button && newScreen == EScreens.discord)
        {
            CheckRedirectButtonClicked(button, discordInviteUrl);
        }
    }

    //redirect to the given url
    private void CheckRedirectButtonClicked(bool button, string url)
    {
        if(button) { Application.OpenURL(url); }
    }

    //get the page data based on the page and the type needed
    private object GetPageData(EPageDataType type)
    {
        string[] returnTypes = new string[8];

        //check the data type, set return types based on data type
        if(type == EPageDataType.header)
        {
            returnTypes = new string[] { welcomePageHeader, quickStartHeader, bestPracticesHeader, templatesHeader, faqHeader, sponsorHeader, changelogHeader };
        }
        else if(type == EPageDataType.description)
        {
            returnTypes = new string[] { welcomePageDescription, quickStartDescription, bestPracticesDescription, templatesDescription, faqDescription, sponsorDescription, changelogDescription };
        }
        else if(type == EPageDataType.redirectButtonTitle)
        {
            returnTypes = new string[] { welcomePageButtonTitle, quickStartPageButtonTitle, bestPracticesPageButtonTitle, templatesPageButtonTitle, faqPageButtonTitle, sponsorPageButtonTitle, changelogPageButtonTitle };
        }
        else if(type == EPageDataType.redirectButtonUrl)
        {
            returnTypes = new string[] { welcomePageUrl, quickStartUrl, bestPracticesUrl, templatesUrl, faqUrl, sponsorUrl, changelogUrl };
        }

        //return results based on the current page
        if (currentScreen == EScreens.welcome) { return returnTypes[0]; }
        else if (currentScreen == EScreens.quickstart) { return returnTypes[1]; }
        else if (currentScreen == EScreens.bestpractices) { return returnTypes[2]; }
        else if (currentScreen == EScreens.templates) { return returnTypes[3]; }
        else if (currentScreen == EScreens.faq) { return returnTypes[4]; }
        else if (currentScreen == EScreens.sponsor) { return returnTypes[5]; }
        else if (currentScreen == EScreens.changelog) { return returnTypes[6]; }

        return "You forgot to update GetPageData()";
    }
}