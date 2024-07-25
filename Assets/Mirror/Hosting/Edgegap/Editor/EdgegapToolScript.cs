using UnityEngine;
using Edgegap;
using IO.Swagger.Model;

/// <summary>
/// This script acts as an interface to display and use the necessary variables from the Edgegap tool.
/// The server info can be accessed from the tool window, as well as through the public script property.
/// </summary>
public class EdgegapToolScript : MonoBehaviour
{
    public Status ServerStatus => EdgegapServerDataManager.GetServerStatus();
}
