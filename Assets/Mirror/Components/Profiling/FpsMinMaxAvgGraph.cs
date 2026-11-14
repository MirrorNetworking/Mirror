using System;
using UnityEngine;
namespace Mirror
{
    public class FpsMinMaxAvgGraph : BaseUIGraph
    {
        protected override void CollectData(int category, out float value, out GraphAggregationMode mode)
        {
            value = 1 / Time.deltaTime;
            switch (category)
            {
                case 0:
                    mode = GraphAggregationMode.Average;
                    break;
                case 1:
                    mode = GraphAggregationMode.Min;
                    break;
                case 2:
                    mode = GraphAggregationMode.Max;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"{category} is not valid.");
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (CategoryColors.Length != 3)
                CategoryColors = new[]
                {
                    Color.cyan, // avg
                    Color.red,  // min
                    Color.green // max
                };

            IsStacked = false;
        }
    }
}
