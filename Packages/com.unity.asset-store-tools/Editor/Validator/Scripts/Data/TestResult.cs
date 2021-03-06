using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace AssetStoreTools.Validator.Data
{
    [Serializable]
    internal struct TestResult
    {
        public ResultStatus Result;

        [SerializeField, HideInInspector]
        private List<TestResultMessage> Messages;

        public int MessageCount => Messages?.Count ?? 0;

        public void AddMessage(string msg)
        {
            AddMessage(msg, null, null);
        }

        public void AddMessage(string msg, IMessageAction clickAction)
        {
            AddMessage(msg, clickAction, null);
        }

        public void AddMessage(string msg, IMessageAction clickAction, params UnityEngine.Object[] messageObjects)
        {
            if (Messages == null)
                Messages = new List<TestResultMessage>();

            var message = new TestResultMessage(msg, clickAction);
            if (messageObjects != null)
                foreach (var obj in messageObjects)
                    message.AddMessageObject(obj);

            Messages.Add(message);
        }

        public TestResultMessage GetMessage(int index)
        {
            if (Messages == null || index >= Messages.Count)
                throw new InvalidOperationException();
            return Messages[index];
        }

        public enum ResultStatus
        {
            Undefined = 0,
            Pass = 1,
            Fail = 2,
            Warning = 3,
            VariableSeverityIssue = 4
        }

        [Serializable]
        internal class TestResultMessage : ISerializationCallbackReceiver
        {
            [SerializeField, HideInInspector]
            private string Text;
            [SerializeField, HideInInspector]
            private List<string> MessageObjects;
            // Serialization
            [SerializeField, HideInInspector]
            private string SerializedClickAction;
            
            private IMessageAction _clickAction;

            public IMessageAction ClickAction => _clickAction;

            public TestResultMessage() { }

            public TestResultMessage(string text)
            {
                Text = text;
            }

            public TestResultMessage(string text, IMessageAction clickAction) : this(text)
            {
                _clickAction = clickAction;
            }

            public void AddMessageObject(Object obj)
            {
                if (MessageObjects == null)
                    MessageObjects = new List<string>();
                var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
                if (globalObjectId != "GlobalObjectId_V1-0-00000000000000000000000000000000-0-0")
                    MessageObjects.Add(GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString());
                else
                    Text += $"\n{obj.name}";
            }

            public Object[] GetMessageObjects()
            {
                if (MessageObjects == null)
                    return Array.Empty<Object>();
                var objects = new Object[MessageObjects.Count];
                for (int i = 0; i < objects.Length; i++)
                {
                    GlobalObjectId.TryParse(MessageObjects[i], out GlobalObjectId id);
                    objects[i] = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                }
                return objects;
            }

            public void OnBeforeSerialize()
            {
                SerializedClickAction = ((int)ClickActionType.None).ToString();
                switch (_clickAction)
                {
                    case MessageActionHighlight action:
                        var objectId = action.GlobalObjectIdentifier.ToString();
                        SerializedClickAction = $"{(int)ClickActionType.HighlightObject}|{objectId}";
                        break;
                    case MessageActionOpenAsset action:
                        objectId = action.GlobalObjectIdentifier.ToString();
                        SerializedClickAction = $"{(int)ClickActionType.OpenAsset}|{objectId}|{action.LineNumber}";
                        break;
                }
            }

            public void OnAfterDeserialize()
            {
                string[] splitAction = SerializedClickAction.Split('|');
                bool parsed = Enum.TryParse(splitAction[0], out ClickActionType clickActionType);
                if (!parsed) return;

                switch (clickActionType)
                {
                    case ClickActionType.HighlightObject:
                        _clickAction = new MessageActionHighlight(splitAction[1]);
                        break;
                    case ClickActionType.OpenAsset:
                        _clickAction = new MessageActionOpenAsset(splitAction[1])
                        { LineNumber = Convert.ToInt32(splitAction[2]) };
                        break;
                    case ClickActionType.None:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public string GetText()
            {
                return Text;
            }
        }
    }
}