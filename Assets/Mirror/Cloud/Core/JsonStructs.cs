using System;

namespace Mirror.Cloud
{
    [System.Serializable]
    public struct CreatedIdJson : ICanBeJson
    {
        public string id;
    }

    [System.Serializable]
    public struct ErrorJson : ICanBeJson
    {
        public string code;
        public string message;

        public int HtmlCode => int.Parse(code);
    }

    [System.Serializable]
    public struct EmptyJson : ICanBeJson
    {
    }
}
