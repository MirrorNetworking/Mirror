using System;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Mirror.Profiler.Table
{

    [Serializable]
    internal class MyTreeElement : TreeElement
    {
        public NetworkProfileMessage message;

        public MyTreeElement(NetworkProfileMessage message, int depth, int id) : base(depth, id)
        {
            this.message = message;
        }

        public override string name => message.Name ?? message.Type ?? "";
    }
}
