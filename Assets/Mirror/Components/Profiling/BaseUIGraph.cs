using System;
using UnityEngine;
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

        float[] data;
        float[] currentData;
        GraphAggregationMode[] currentModes;
        // for avg aggregation mode
        int[] currentCounts;
        int dataStart;
        bool dirty;
        float pointTime;
        Material material;

        int DataLastIndex => (dataStart - 1 + Points) % Points;

        void Awake()
        {
            Renderer.material = material = Instantiate(Material);
            data = new float[Points * CategoryColors.Length];
            currentData = new float[CategoryColors.Length];
            currentCounts = new int[CategoryColors.Length];
            currentModes = new GraphAggregationMode[CategoryColors.Length];
            dirty = true;
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

                if (mode != currentModes[i])
                {
                    currentModes[i] = mode;
                    ResetCurrent(i);
                }

                switch (mode)
                {
                    case GraphAggregationMode.Average:
                    case GraphAggregationMode.Sum:
                    case GraphAggregationMode.PerSecond:
                        currentData[i] += value;
                        currentCounts[i]++;
                        break;
                    case GraphAggregationMode.Min:
                        if (currentData[i] > value)
                            currentData[i] = value;

                        break;
                    case GraphAggregationMode.Max:
                        if (value > currentData[i])
                            currentData[i] = value;

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            pointTime += Time.deltaTime;
            if (pointTime > SecondsPerPoint)
            {
                dataStart = (dataStart + 1) % Points;
                ClearDataAt(DataLastIndex);
                for (int i = 0; i < CategoryColors.Length; i++)
                {
                    float value = currentData[i];
                    switch (currentModes[i])
                    {
                        case GraphAggregationMode.Sum:
                        case GraphAggregationMode.Min:
                        case GraphAggregationMode.Max:
                            // do nothing!
                            break;
                        case GraphAggregationMode.Average:
                            value /= currentCounts[i];
                            break;
                        case GraphAggregationMode.PerSecond:
                            value /= pointTime;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    SetCurrentGraphData(i, value);
                    ResetCurrent(i);
                }

                pointTime = 0;
            }
        }

        void ResetCurrent(int i)
        {
            switch (currentModes[i])
            {
                case GraphAggregationMode.Min:
                    currentData[i] = float.MaxValue;
                    break;
                default:
                    currentData[i] = 0;
                    break;
            }

            currentCounts[i] = 0;
        }

        protected virtual string FormatValue(float value) => $"{value:N1}";

        protected abstract void CollectData(int category, out float value, out GraphAggregationMode mode);

        void SetCurrentGraphData(int c, float value)
        {
            data[DataLastIndex * CategoryColors.Length + c] = value;
            dirty = true;
        }

        void ClearDataAt(int i)
        {
            for (int c = 0; c < CategoryColors.Length; c++)
            {
                data[i * CategoryColors.Length + c] = 0;
            }

            dirty = true;
        }

        public void LateUpdate()
        {
            if (dirty)
            {
                material.SetInt(Width, Points);
                material.SetInt(DataStart, dataStart);
                float max = 1;
                if (IsStacked)
                    for (int x = 0; x < Points; x++)
                    {
                        float total = 0;
                        for (int c = 0; c < CategoryColors.Length; c++)
                        {
                            total += data[x * CategoryColors.Length + c];
                        }

                        if (total > max)
                            max = total;
                    }
                else
                    for (int i = 0; i < data.Length; i++)
                    {
                        float v = data[i];
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

                material.SetFloat(MaxValue, max);
                material.SetFloatArray(GraphData, data);
                material.SetInt(CategoryCount, CategoryColors.Length);
                material.SetColorArray(Colors, CategoryColors);
                dirty = false;
            }
        }

        protected virtual float AdjustMaxValue(float max) => Mathf.Ceil(max);
    }
}
