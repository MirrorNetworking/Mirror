using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
namespace Mirror
{
    public enum GraphAggregationMode
    {
        Sum,
        Average,
        PerSecond,
        Min,
        Max
    }

    public abstract class BaseUIGraph : MonoBehaviour
    {
        static readonly int MaxValue = Shader.PropertyToID("_MaxValue");
        static readonly int GraphData = Shader.PropertyToID("_GraphData");
        static readonly int CategoryCount = Shader.PropertyToID("_CategoryCount");
        static readonly int Colors = Shader.PropertyToID("_CategoryColors");
        static readonly int Width = Shader.PropertyToID("_Width");
        static readonly int DataStart = Shader.PropertyToID("_DataStart");

        public Material Material;
        public Graphic Renderer;
        [Range(1, 64)]
        public int Points = 64;
        public float SecondsPerPoint = 1;
        public Color[] CategoryColors = new[] { Color.cyan };
        public bool IsStacked;

        public Text[] LegendTexts;
        [Header("Diagnostics")]
        [ReadOnly, SerializeField]
        Material runtimeMaterial;

        float[] graphData;
        // graphData is a circular buffer, this is the offset to get the 0-index
        int graphDataStartIndex;
        // Is graphData dirty and needs to be set to the material
        bool isGraphDataDirty;
        // currently aggregating data to be added to the graph soon
        float[] aggregatingData;
        GraphAggregationMode[] aggregatingModes;
        // Counts for avg aggregation mode
        int[] aggregatingDataCounts;
        // How much time has elapsed since the last aggregation finished
        float aggregatingTime;

        int DataLastIndex => (graphDataStartIndex - 1 + Points) % Points;

        void Awake()
        {
            Renderer.material = runtimeMaterial = Instantiate(Material);
            graphData = new float[Points * CategoryColors.Length];
            aggregatingData = new float[CategoryColors.Length];
            aggregatingDataCounts = new int[CategoryColors.Length];
            aggregatingModes = new GraphAggregationMode[CategoryColors.Length];
            isGraphDataDirty = true;
        }

        protected virtual void OnValidate()
        {
            if (Renderer == null)
                Renderer = GetComponent<Graphic>();
        }

        protected virtual void Update()
        {
            for (int i = 0; i < CategoryColors.Length; i++)
            {
                CollectData(i, out float value, out GraphAggregationMode mode);
                // we probably don't need negative values, so lets skip supporting it
                if (value < 0)
                {
                    Debug.LogWarning("Graphing negative values is not supported.");
                    value = 0;
                }

                if (mode != aggregatingModes[i])
                {
                    aggregatingModes[i] = mode;
                    ResetCurrent(i);
                }

                switch (mode)
                {
                    case GraphAggregationMode.Average:
                    case GraphAggregationMode.Sum:
                    case GraphAggregationMode.PerSecond:
                        aggregatingData[i] += value;
                        aggregatingDataCounts[i]++;
                        break;
                    case GraphAggregationMode.Min:
                        if (aggregatingData[i] > value)
                            aggregatingData[i] = value;
                        break;
                    case GraphAggregationMode.Max:
                        if (value > aggregatingData[i])
                            aggregatingData[i] = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            aggregatingTime += Time.deltaTime;
            if (aggregatingTime > SecondsPerPoint)
            {
                graphDataStartIndex = (graphDataStartIndex + 1) % Points;
                ClearDataAt(DataLastIndex);
                for (int i = 0; i < CategoryColors.Length; i++)
                {
                    float value = aggregatingData[i];
                    switch (aggregatingModes[i])
                    {
                        case GraphAggregationMode.Sum:
                        case GraphAggregationMode.Min:
                        case GraphAggregationMode.Max:
                            // do nothing!
                            break;
                        case GraphAggregationMode.Average:
                            value /= aggregatingDataCounts[i];
                            break;
                        case GraphAggregationMode.PerSecond:
                            value /= aggregatingTime;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    SetCurrentGraphData(i, value);
                    ResetCurrent(i);
                }

                aggregatingTime = 0;
            }
        }

        void ResetCurrent(int i)
        {
            switch (aggregatingModes[i])
            {
                case GraphAggregationMode.Min:
                    aggregatingData[i] = float.MaxValue;
                    break;
                default:
                    aggregatingData[i] = 0;
                    break;
            }

            aggregatingDataCounts[i] = 0;
        }

        protected virtual string FormatValue(float value) => $"{value:N1}";

        protected abstract void CollectData(int category, out float value, out GraphAggregationMode mode);

        void SetCurrentGraphData(int c, float value)
        {
            graphData[DataLastIndex * CategoryColors.Length + c] = value;
            isGraphDataDirty = true;
        }

        void ClearDataAt(int i)
        {
            for (int c = 0; c < CategoryColors.Length; c++)
                graphData[i * CategoryColors.Length + c] = 0;

            isGraphDataDirty = true;
        }

        public void LateUpdate()
        {
            if (isGraphDataDirty)
            {
                runtimeMaterial.SetInt(Width, Points);
                runtimeMaterial.SetInt(DataStart, graphDataStartIndex);
                float max = 1;
                if (IsStacked)
                    for (int x = 0; x < Points; x++)
                    {
                        float total = 0;
                        for (int c = 0; c < CategoryColors.Length; c++)
                            total += graphData[x * CategoryColors.Length + c];

                        if (total > max)
                            max = total;
                    }
                else
                    for (int i = 0; i < graphData.Length; i++)
                    {
                        float v = graphData[i];
                        if (v > max)
                            max = v;
                    }

                max = AdjustMaxValue(max);
                for (int i = 0; i < LegendTexts.Length; i++)
                {
                    Text legendText = LegendTexts[i];
                    float pct = (float)i / (LegendTexts.Length - 1);
                    legendText.text = FormatValue(max * pct);
                }

                runtimeMaterial.SetFloat(MaxValue, max);
                runtimeMaterial.SetFloatArray(GraphData, graphData);
                runtimeMaterial.SetInt(CategoryCount, CategoryColors.Length);
                runtimeMaterial.SetColorArray(Colors, CategoryColors);
                isGraphDataDirty = false;
            }
        }

        protected virtual float AdjustMaxValue(float max) => Mathf.Ceil(max);
    }
}
