using UnityEngine;

namespace Mirror.Tests.EditorBehaviours.Attributes
{
    public class AttributeBehaviour_MonoBehaviour : MonoBehaviour
    {
        public static readonly float Expected_float = 2020f;

        public static readonly ClassWithNoConstructor Expected_ClassWithNoConstructor =
            new ClassWithNoConstructor { a = 10 };

        public static readonly ClassWithConstructor Expected_ClassWithConstructor = new ClassWithConstructor(29);

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

        [Client]
        public ClassWithNoConstructor Client_ClassWithNoConstructor_Function()
        {
            return Expected_ClassWithNoConstructor;
        }

        [Client]
        public void Client_ClassWithNoConstructor_out_Function(out ClassWithNoConstructor value)
        {
            value = Expected_ClassWithNoConstructor;
        }

        [Client]
        public ClassWithConstructor Client_ClassWithConstructor_Function()
        {
            return Expected_ClassWithConstructor;
        }

        [Client]
        public void Client_ClassWithConstructor_out_Function(out ClassWithConstructor value)
        {
            value = Expected_ClassWithConstructor;
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

        [Server]
        public ClassWithNoConstructor Server_ClassWithNoConstructor_Function()
        {
            return Expected_ClassWithNoConstructor;
        }

        [Server]
        public void Server_ClassWithNoConstructor_out_Function(out ClassWithNoConstructor value)
        {
            value = Expected_ClassWithNoConstructor;
        }

        [Server]
        public ClassWithConstructor Server_ClassWithConstructor_Function()
        {
            return Expected_ClassWithConstructor;
        }

        [Server]
        public void Server_ClassWithConstructor_out_Function(out ClassWithConstructor value)
        {
            value = Expected_ClassWithConstructor;
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

        [ClientCallback]
        public ClassWithNoConstructor ClientCallback_ClassWithNoConstructor_Function()
        {
            return Expected_ClassWithNoConstructor;
        }

        [ClientCallback]
        public void ClientCallback_ClassWithNoConstructor_out_Function(out ClassWithNoConstructor value)
        {
            value = Expected_ClassWithNoConstructor;
        }

        [ClientCallback]
        public ClassWithConstructor ClientCallback_ClassWithConstructor_Function()
        {
            return Expected_ClassWithConstructor;
        }

        [ClientCallback]
        public void ClientCallback_ClassWithConstructor_out_Function(out ClassWithConstructor value)
        {
            value = Expected_ClassWithConstructor;
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

        [ServerCallback]
        public ClassWithNoConstructor ServerCallback_ClassWithNoConstructor_Function()
        {
            return Expected_ClassWithNoConstructor;
        }

        [ServerCallback]
        public void ServerCallback_ClassWithNoConstructor_out_Function(out ClassWithNoConstructor value)
        {
            value = Expected_ClassWithNoConstructor;
        }

        [ServerCallback]
        public ClassWithConstructor ServerCallback_ClassWithConstructor_Function()
        {
            return Expected_ClassWithConstructor;
        }

        [ServerCallback]
        public void ServerCallback_ClassWithConstructor_out_Function(out ClassWithConstructor value)
        {
            value = Expected_ClassWithConstructor;
        }
    }
}
