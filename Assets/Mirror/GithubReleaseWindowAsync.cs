

#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;

public class GitHubReleaseWindowAsync : EditorWindow
{
    GitHubRelease currentRelease;
    Vector2 scrollPosition;
    bool isRefreshing = false;
    DateTime lastRefreshTime = DateTime.Now;
    byte secondsUntilRefresh = 30;
    GUIStyle labelStyle;
    GUIStyle valueStyle;
    string downloadedFilePath = "";
    bool downloadInProgress = false;

    [MenuItem("Tools/Mirror/Get Latest Release")]
    static void ShowWindow()
    {
        var window = GetWindow<GitHubReleaseWindowAsync>("Mirror Latest Release");
        window.minSize = new Vector2(510, 250);
        window.FetchReleaseDataAsync();
    }

    void OnEnable()
    {
        // if window is open when Unity recompiles, this can throw an error
        try
        {
            labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 5, 5),
                fixedHeight = 25
            };
            labelStyle.CalcMinMaxWidth(new GUIContent("Sample Text"), out float minLabelWidth, out float maxLabelWidth);

            valueStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 5, 5),
                fixedHeight = 25
            };

            EditorApplication.update += UpdateRefreshTimer;
            FetchReleaseDataAsync();
        }
        catch { }
    }

    void OnDisable()
    {
        EditorApplication.update -= UpdateRefreshTimer;
    }

    void UpdateRefreshTimer()
    {
        if (lastRefreshTime != DateTime.MinValue)
        {
            secondsUntilRefresh = (byte)Mathf.Max(0, (float)(30 - (DateTime.Now - lastRefreshTime).TotalSeconds));
            Repaint();
        }
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);

        using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition))
        {
            scrollPosition = scroll.scrollPosition;

            if (currentRelease != null)
                DrawReleaseInfo();
            else if (!isRefreshing)
                EditorGUILayout.HelpBox("No release data available", MessageType.Info);
        }

        DrawBottomButtons();
    }

    void DrawReleaseInfo()
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.largeLabel) { fontSize = 14, richText = true };

        EditorGUILayout.LabelField("<b>Release Information</b>", headerStyle);
        EditorGUILayout.Space(5);

        DrawPair("Version", currentRelease.tag_name);
        DrawPair("Published", DateTime.Parse(currentRelease.published_at).ToLocalTime().ToString("g"));
        DrawPair("Release Page", currentRelease.html_url);
        //DrawPair("Title", currentRelease.name);
        //DrawPair("Pre-release", currentRelease.prerelease ? "Yes" : "No");
        //DrawPair("Author", currentRelease.author.login);
        //DrawPair("Description", currentRelease.body);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("<b>Package</b>", headerStyle);
        EditorGUILayout.Space(5);

        foreach (var asset in currentRelease.assets)
        {
            if (asset.name.Contains("Mirror"))
            {
                DrawPair("Name", asset.name);
                DrawPair("File Size", FormatFileSize(asset.size));
                //DrawPair("Uploaded", DateTime.Parse(asset.created_at).ToLocalTime().ToString("g"));
                //DrawPair("Downloads", asset.download_count.ToString());
                EditorGUILayout.Space(5);
            }
        }
    }

    void DrawPair(string label, string value)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"<b>{label}:</b>", labelStyle, GUILayout.Width(110));
            EditorGUILayout.LabelField(value, valueStyle, GUILayout.ExpandWidth(true));
        }
    }

    void DrawBottomButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            string refreshText = isRefreshing ? "Refreshing..." : "Refresh";
            GUI.enabled = !isRefreshing && secondsUntilRefresh <= 0 && !downloadInProgress;

            if (secondsUntilRefresh > 0)
                refreshText += $" [{secondsUntilRefresh}]";

            if (GUILayout.Button(refreshText, GUILayout.Height(30)))
            {
                FetchReleaseDataAsync();
                lastRefreshTime = DateTime.Now;
                downloadedFilePath = "";
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            GUI.enabled = currentRelease != null && !isRefreshing && !downloadInProgress;
            if (GUILayout.Button("Download Latest Package", GUILayout.Height(30), GUILayout.Width(200)))
                DownloadLatestPackageAsync();
            GUI.enabled = true;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !string.IsNullOrWhiteSpace(downloadedFilePath);
            EditorGUILayout.LabelField("File Path:", GUILayout.Width(70));
            EditorGUILayout.SelectableLabel(downloadedFilePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (GUILayout.Button("Copy Path", GUILayout.Width(80)) && !string.IsNullOrEmpty(downloadedFilePath))
                EditorGUIUtility.systemCopyBuffer = downloadedFilePath;
            GUI.enabled = true;
        }

        EditorGUILayout.Space(5);
    }

    async void FetchReleaseDataAsync()
    {
        isRefreshing = true;
        string apiUrl = "https://api.github.com/repos/MirrorNetworking/Mirror/releases/latest";

        try
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(apiUrl))
            {
                webRequest.SetRequestHeader("User-Agent", "UnityEditor");
                webRequest.SetRequestHeader("Accept", "application/vnd.github.v3+json");

                var operation = webRequest.SendWebRequest();

                while (!operation.isDone)
                    await Task.Yield();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        currentRelease = JsonUtility.FromJson<GitHubRelease>(webRequest.downloadHandler.text);
                        Repaint();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"JSON Parse Error: {ex.Message}");
                        ShowNotification(new GUIContent($"Data parse failed: {ex.Message}"));
                    }

                    lastRefreshTime = DateTime.Now;
                }
                else
                {
                    Debug.LogError($"Network Error: {webRequest.error}");
                    ShowNotification(new GUIContent($"Fetch failed: {webRequest.error}"));
                }
            }
        }
        finally
        {
            isRefreshing = false;
        }
    }

    async void DownloadLatestPackageAsync()
    {
        if (currentRelease == null || currentRelease.assets.Length == 0) return;

        var mirrorAsset = Array.Find(currentRelease.assets, a => a.name.Contains("Mirror"));
        if (mirrorAsset == null) return;

        string downloadURL = mirrorAsset.browser_download_url;
        string fileName = Path.GetFileName(downloadURL);
        string downloadPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), fileName);

        downloadInProgress = true;
        try
        {
            using (UnityWebRequest downloadRequest = UnityWebRequest.Get(downloadURL))
            {
                downloadRequest.downloadHandler = new DownloadHandlerFile(downloadPath);

                var operation = downloadRequest.SendWebRequest();

                while (!operation.isDone)
                    await Task.Yield();

                if (downloadRequest.result == UnityWebRequest.Result.Success)
                {
                    downloadedFilePath = downloadPath;
                    AssetDatabase.Refresh();
                    Repaint();
                    ShowNotification(new GUIContent("Download complete!"));
                }
                else
                {
                    Debug.LogError($"Download Failed: {downloadRequest.error}");
                    ShowNotification(new GUIContent($"Download failed: {downloadRequest.error}"));
                }
            }
        }
        finally
        {
            downloadInProgress = false;
        }
    }

    string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        while (bytes >= 1024 && order < sizes.Length - 1)
        {
            order++;
            bytes = bytes / 1024;
        }
        return $"{bytes:0.##} {sizes[order]}";
    }

    #region Serialzable Classes

    [Serializable]
    public class GitHubRelease
    {
        public string tag_name;
        public string name;
        public string published_at;
        public bool draft;
        public bool prerelease;
        public string body;
        public string html_url;
        public Author author;
        public Asset[] assets;
    }

    [Serializable]
    public class Author
    {
        public string login;
        public string avatar_url;
    }

    [Serializable]
    public class Asset
    {
        public string name;
        public string browser_download_url;
        public string created_at;
        public string content_type;
        public int size;
        public int download_count;
    }

    #endregion
}

#endif

