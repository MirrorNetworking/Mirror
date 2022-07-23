// Android NetworkDiscovery Multicast fix
// https://github.com/vis2k/Mirror/pull/2887
using UnityEditor;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Xml;
using System.IO;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif


[InitializeOnLoad]
public class AndroidManifestHelper : IPreprocessBuildWithReport, IPostprocessBuildWithReport
#if UNITY_ANDROID
	, IPostGenerateGradleAndroidProject
#endif
{
    public int callbackOrder { get { return 99999; } }

#if UNITY_ANDROID
    public void OnPostGenerateGradleAndroidProject(string path)
	{
        string manifestFolder = Path.Combine(path, "src/main");
        string sourceFile = manifestFolder + "/AndroidManifest.xml";
        // Load android manifest file
        XmlDocument doc = new XmlDocument();
        doc.Load(sourceFile);

        string androidNamepsaceURI;
        XmlElement element = (XmlElement)doc.SelectSingleNode("/manifest");
        if (element == null)
        {
            UnityEngine.Debug.LogError("Could not find manifest tag in android manifest.");
            return;
        }

        // Get android namespace URI from the manifest
        androidNamepsaceURI = element.GetAttribute("xmlns:android");
        if (string.IsNullOrEmpty(androidNamepsaceURI))
        {
            UnityEngine.Debug.LogError("Could not find Android Namespace in manifest.");
            return;
        }
        AddOrRemoveTag(doc,
               androidNamepsaceURI,
               "/manifest",
               "uses-permission",
               "android.permission.CHANGE_WIFI_MULTICAST_STATE",
               true,
               false);
        AddOrRemoveTag(doc,
               androidNamepsaceURI,
               "/manifest",
               "uses-permission",
               "android.permission.INTERNET",
               true,
               false);
        doc.Save(sourceFile);
    }
#endif

    static void AddOrRemoveTag(XmlDocument doc, string @namespace, string path, string elementName, string name, bool required, bool modifyIfFound, params string[] attrs) // name, value pairs
    {
        var nodes = doc.SelectNodes(path + "/" + elementName);
        XmlElement element = null;
        foreach (XmlElement e in nodes)
        {
            if (name == null || name == e.GetAttribute("name", @namespace))
            {
                element = e;
                break;
            }
        }

        if (required)
        {
            if (element == null)
            {
                var parent = doc.SelectSingleNode(path);
                element = doc.CreateElement(elementName);
                element.SetAttribute("name", @namespace, name);
                parent.AppendChild(element);
            }

            for (int i = 0; i < attrs.Length; i += 2)
            {
                if (modifyIfFound || string.IsNullOrEmpty(element.GetAttribute(attrs[i], @namespace)))
                {
                    if (attrs[i + 1] != null)
                    {
                        element.SetAttribute(attrs[i], @namespace, attrs[i + 1]);
                    }
                    else
                    {
                        element.RemoveAttribute(attrs[i], @namespace);
                    }
                }
            }
        }
        else
        {
            if (element != null && modifyIfFound)
            {
                element.ParentNode.RemoveChild(element);
            }
        }
    }

    public void OnPostprocessBuild(BuildReport report) {}
	public void OnPreprocessBuild(BuildReport report) {}
}
