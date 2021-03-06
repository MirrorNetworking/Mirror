using UnityEditor;
using UnityEngine;

namespace AssetStoreTools.Validator.Data
{
    internal interface IMessageAction
    {
        string ActionTooltip { get; }

        void Execute();
    }

    internal enum ClickActionType
    {
        None = 0,
        HighlightObject = 1,
        OpenAsset = 2
    }

    internal class MessageActionHighlight : IMessageAction
    {
        private Object _objectToHighlight;

        public GlobalObjectId GlobalObjectIdentifier { get; set; }
        public string ActionTooltip => "Click to highlight the associated object in Hierarchy/Project view";

        public MessageActionHighlight(Object objectToHighlight)
        {
            this._objectToHighlight = objectToHighlight;
            GlobalObjectIdentifier = GlobalObjectId.GetGlobalObjectIdSlow(objectToHighlight);
        }

        public MessageActionHighlight(string globalObjectId)
        {
            GlobalObjectId.TryParse(globalObjectId, out GlobalObjectId globalObjectIdentifier);
            GlobalObjectIdentifier = globalObjectIdentifier;
        }

        public void Execute()
        {
            if(_objectToHighlight == null)
                _objectToHighlight = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(GlobalObjectIdentifier);
            
            EditorGUIUtility.PingObject(_objectToHighlight);
        }
    }

    internal class MessageActionOpenAsset : IMessageAction
    {
        private Object _objectToOpen;
        public int LineNumber { get; set; }

        public GlobalObjectId GlobalObjectIdentifier { get; set; }
        public string ActionTooltip => "Click to open the associated asset";

        public MessageActionOpenAsset(Object objectToOpen)
        {
            this._objectToOpen = objectToOpen;
            GlobalObjectIdentifier = GlobalObjectId.GetGlobalObjectIdSlow(objectToOpen);
        }

        public MessageActionOpenAsset(string globalObjectId)
        {
            GlobalObjectId.TryParse(globalObjectId, out GlobalObjectId globalObjectIdentifier);
            GlobalObjectIdentifier = globalObjectIdentifier;
        }

        public void Execute()
        {
            if (_objectToOpen == null)
                _objectToOpen = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(GlobalObjectIdentifier);
            AssetDatabase.OpenAsset(_objectToOpen, LineNumber);
        }
    }
}