using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;


namespace Mirror.Profiler.Table
{

    static class MyTreeElementGenerator
    {
        internal static IList<MyTreeElement> GenerateTable(NetworkProfileTick tick)
        {
            int IDCounter = 0;

            List<MyTreeElement> treeElements = new List<MyTreeElement>(tick.Messages.Count);
            MyTreeElement root = new MyTreeElement(new NetworkProfileMessage(), -1, IDCounter++);
            
            treeElements.Add(root);

            foreach (var message in tick.Messages)
            {
                MyTreeElement child = new MyTreeElement(message, 0, IDCounter++);
                treeElements.Add(child); 
            }

            return treeElements;
        }
    }
}
