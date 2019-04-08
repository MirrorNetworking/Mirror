using System;
using UnityEngine;

namespace Mirror
{
    public class ErrorMessage : ByteMessage {}

    public class ReadyMessage : EmptyMessage {}

    public class NotReadyMessage : EmptyMessage {}

    public class AddPlayerMessage : BytesMessage {}

    public class RemovePlayerMessage : EmptyMessage {}

    public class DisconnectMessage : EmptyMessage {}

    public class ConnectMessage : EmptyMessage {}

    public class SceneMessage : StringMessage
    {
        public SceneMessage(string value) : base(value) {}

        public SceneMessage() {}
    }
}
