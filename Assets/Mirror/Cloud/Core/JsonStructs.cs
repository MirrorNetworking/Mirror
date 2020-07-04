using System;

namespace Mirror.Cloud
{
    [Serializable]
    public struct CreatedIdJson : ICanBeJson
    {
        public string id;
    }

    [Serializable]
    public struct ErrorJson : ICanBeJson
    {
        public string code;
        public string message;

        public int HtmlCode => int.Parse(code);
    }

    [Serializable]
    public struct EmptyJson : ICanBeJson
    {
    }
}
