using IO.Swagger.Model;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Edgegap
{
    // MIRROR CHANGE: EdgegapServerDataManagerUtils were merged into EdgegapServerDataManager to reduce complexity & dependencies
    // static class EdgegapServerDataManagerUtils {}
    // END MIRROR CHANGE

    /// <summary>
    /// Utility class to centrally manage the Edgegap server data.
    /// </summary>
    //
    public static class EdgegapServerDataManager
    {
        // MIRROR CHANGE: ServerDataManager.GetServerStatus() is still static for other scripts to access.
        // However, all UI code was moved to non-static EdgegapWindow.
        // this allows us to properly assign the stylesheet without hardcoding paths etc.
        internal static Status _serverData;
        public static Status GetServerStatus() => _serverData;
    }
}
