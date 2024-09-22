using System;
using UnityEngine;
namespace Mirror
{
    public class NetworkPingGraph : BaseUIGraph
    {
        protected override void CollectData(int category, out float value, out GraphAggregationMode mode)
        {
            mode = GraphAggregationMode.Average;
            switch (category)
            {
                case 0:
                    value = (float)NetworkTime.rtt * 1000f;
                    break;
                case 1:
                    value = (float)NetworkTime.rttVariance * 1000f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"{category} is not valid.");
            }
        }

        protected override string FormatValue(float value) => $"{value:N0}ms";

        protected override void OnValidate()
        {
            base.OnValidate();
            if (CategoryColors.Length != 2)
                CategoryColors = new[] { Color.cyan, Color.yellow };

            IsStacked = false;
        }
    }
}
