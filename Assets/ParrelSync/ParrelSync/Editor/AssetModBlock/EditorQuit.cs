using UnityEditor;
namespace ParrelSync
{
    [InitializeOnLoad]
    public class EditorQuit
    {
        /// <summary>
        /// Is editor being closed
        /// </summary>
        static public bool IsQuiting { get; private set; }
        static void Quit()
        {
            IsQuiting = true;
        }

        static EditorQuit()
        {
            IsQuiting = false;
            EditorApplication.quitting += Quit;
        }
    }
}