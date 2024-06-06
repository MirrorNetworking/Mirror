using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Profiler.Chart
{

    public interface ISeries
    {
        IEnumerable<(int, float)> Data { get; }

        string Name { get; }
    }
}
