using System;
using UnityEngine;
namespace Mirror
{
    public class NetworkUsageGraph : BaseUIGraph
    {
        int dataIn;
        int dataOut;

        void Start()
        {
            // Ordering, Awake happens before NetworkDiagnostics reset
            NetworkDiagnostics.InMessageEvent += OnReceive;
            NetworkDiagnostics.OutMessageEvent += OnSend;
        }

        void OnEnable()
        {
            // If we've been inactive, clear counter
            dataIn = 0;
            dataOut = 0;
        }

        void OnDestroy()
        {
            NetworkDiagnostics.InMessageEvent -= OnReceive;
            NetworkDiagnostics.OutMessageEvent -= OnSend;
        }

        void OnSend(NetworkDiagnostics.MessageInfo obj) => dataOut += obj.bytes;

        void OnReceive(NetworkDiagnostics.MessageInfo obj) => dataIn += obj.bytes;

        protected override void CollectData(int category, out float value, out GraphAggregationMode mode)
        {
            mode = GraphAggregationMode.PerSecond;
            switch (category)
            {
                case 0:
                    value = dataIn;
                    dataIn = 0;
                    break;
                case 1:
                    value = dataOut;
                    dataOut = 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"{category} is not valid.");
            }
        }

        static readonly string[] Units = new[] { "B/s", "KiB/s", "MiB/s" };
        const float UnitScale = 1024;

        protected override string FormatValue(float value)
        {
            string selectedUnit = null;
            for (int i = 0; i < Units.Length; i++)
            {
                string unit = Units[i];
                selectedUnit = unit;
                if (i > 0)
                    value /= UnitScale;

                if (value < UnitScale)
                    break;
            }

            return $"{value:N0} {selectedUnit}";
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (CategoryColors.Length != 2)
                CategoryColors = new[]
                {
                    Color.red,  // min
                    Color.green // max
                };

            IsStacked = false;
        }
    }
}
