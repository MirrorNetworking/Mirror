// NetworkServer statics wrap around a NetworkServerComponent for now.
// some day, we can:
// -> rename NetworkServerComponent to NetworkServer (and remove the static one)
// -> keep [Obsolete] NetworkServerComponent : NetworkServer for compatibility.
//
// Having NetworkServer as component brings several advantages:
// + easier to test
// + easier to clean up state
// + easier to inspect/modify in the Inspector
// + possibility of multiple NS/NC later
using UnityEngine;

namespace Mirror
{
    public class NetworkServerComponent : MonoBehaviour
    {

    }
}
