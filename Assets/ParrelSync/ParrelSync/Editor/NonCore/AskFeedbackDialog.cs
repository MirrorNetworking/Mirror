namespace ParrelSync.NonCore
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// A simple script to display feedback/star dialog after certain time of project being opened/re-compiled.
    /// Will only pop-up once unless "Remind me next time" are chosen.
    /// Removing this file from project wont effect any other functions.
    /// </summary>
    [InitializeOnLoad]
    public class AskFeedbackDialog
    {
        const string InitializeOnLoadCountKey = "ParrelSync_InitOnLoadCount", StopShowingKey = "ParrelSync_StopShowFeedBack";
        static AskFeedbackDialog()
        {            
            if (EditorPrefs.HasKey(StopShowingKey)) { return; }

            int InitializeOnLoadCount = EditorPrefs.GetInt(InitializeOnLoadCountKey, 0);
            if (InitializeOnLoadCount > 20)
            {
                ShowDialog();
            }
            else
            {
                EditorPrefs.SetInt(InitializeOnLoadCountKey, InitializeOnLoadCount + 1);
            }
        }

        //[MenuItem("ParrelSync/(Debug)Show AskFeedbackDialog ")]
        private static void ShowDialog()
        {
            int option = EditorUtility.DisplayDialogComplex("Do you like " + ParrelSync.ClonesManager.ProjectName + "?",
                   "Do you like " + ParrelSync.ClonesManager.ProjectName + "?\n" +
                   "If so, please don't hesitate to star it on GitHub and contribute to the project!",
                   "Star on GitHub",
                   "Close",
                   "Remind me next time"
               );

            switch (option)
            {
                // First parameter.
                case 0:
                    Debug.Log("AskFeedbackDialog: Star on GitHub selected");
                    EditorPrefs.SetBool(StopShowingKey, true);
                    EditorPrefs.DeleteKey(InitializeOnLoadCountKey);
                    Application.OpenURL(ExternalLinks.GitHubHome);
                    break;
                // Second parameter.
                case 1:
                    Debug.Log("AskFeedbackDialog: Close and never show again.");
                    EditorPrefs.SetBool(StopShowingKey, true);
                    EditorPrefs.DeleteKey(InitializeOnLoadCountKey);
                    break;
                // Third parameter.
                case 2:
                    Debug.Log("AskFeedbackDialog: Remind me next time");
                    EditorPrefs.SetInt(InitializeOnLoadCountKey, 0);
                    break;
                default:
                    //Debug.Log("Close windows.");
                    break;
            }
        }

        ///// <summary>
        ///// For debug purpose
        ///// </summary>
        //[MenuItem("ParrelSync/(Debug)Delete AskFeedbackDialog keys")]
        //private static void DebugDeleteAllKeys()
        //{
        //    EditorPrefs.DeleteKey(InitializeOnLoadCountKey);
        //    EditorPrefs.DeleteKey(StopShowingKey);
        //    Debug.Log("AskFeedbackDialog keys deleted");
        //}
    }
}