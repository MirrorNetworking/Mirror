using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Mirror.Profiler.Chart
{

    public class Series : ISeries
    {

        public string Name { get; }
        public IEnumerable<(int, float)> Data { get; }

        public Series(string name, IEnumerable<(int, float)> data)
        {
            Name = name;
            Data = data;
        }

        public Rect Bounds
        {
            get
            {
                int minx = int.MaxValue;
                float miny = int.MaxValue;
                int maxx = int.MinValue;
                float maxy = int.MinValue;

                foreach ((int x, float y) in Data)
                {
                    minx = Mathf.Min(minx, x);
                    miny = Mathf.Min(miny, y);
                    maxx = Mathf.Max(maxx, x);
                    maxy = Mathf.Max(maxy, y);
                }

                if (minx > maxx)
                {
                    minx = maxx = 0;
                    miny = maxy = 0;
                }

                return new Rect(minx, miny, maxx - minx, maxy - miny);
            }
        }

    }

}