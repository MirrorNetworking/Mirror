using UnityEngine;

namespace Mirror.Tests.EditorBehaviours.Attributes
{
    [AddComponentMenu("")]
    public class AttributeBehaviour_NetworkBehaviour : NetworkBehaviour
    {
        public static readonly float Expected_float = 2020f;

        [Client]
        public float Client_float_Function()
        {
            return Expected_float;
        }

        [Client]
        public void Client_float_out_Function(out float value)
        {
            value = Expected_float;
        }

        [Server]
        public float Server_float_Function()
        {
            return Expected_float;
        }

        [Server]
        public void Server_float_out_Function(out float value)
        {
            value = Expected_float;
        }

        [ClientCallback]
        public float ClientCallback_float_Function()
        {
            return Expected_float;
        }

        [ClientCallback]
        public void ClientCallback_float_out_Function(out float value)
        {
            value = Expected_float;
        }

        [ServerCallback]
        public float ServerCallback_float_Function()
        {
            return Expected_float;
        }

        [ServerCallback]
        public void ServerCallback_float_out_Function(out float value)
        {
            value = Expected_float;
        }
    }
}
