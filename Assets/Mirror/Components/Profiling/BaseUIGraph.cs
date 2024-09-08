using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;
namespace Mirror
{

    public enum GraphAggregationMode
    {
        Sum,
        Average,
        PerSecond,
        Min,
        Max,
    }
    public abstract class BaseUIGraph : MonoBehaviour
    {
        private static readonly int MaxValue = Shader.PropertyToID("_MaxValue");
        private static readonly int GraphData = Shader.PropertyToID("_GraphData");
        private static readonly int CategoryCount = Shader.PropertyToID("_CategoryCount");
        private static readonly int Colors = Shader.PropertyToID("_CategoryColors");
        private static readonly int Width = Shader.PropertyToID("_Width");
        private static readonly int DataStart = Shader.PropertyToID("_DataStart");

        public Material Material;
        public Graphic Renderer;
        [Range(1, 64)]
        public int Points = 64;
        public float SecondsPerPoint = 1;
        public Color[] CategoryColors = new[]
        {
            Color.cyan
        };
        public bool IsStacked;

        public Text[] LegendTexts;

        private float[] _data;
        private float[] _currentData;
        private GraphAggregationMode[] _currentModes;
        // for avg aggregation mode
        private int[] _currentCounts;
        private int _dataStart;
        private bool _dirty;
        private float _pointTime;
        private Material _material;

        private int DataLastIndex => ((_dataStart - 1) + Points) % Points;

        private void Awake()
        {
            Renderer.material = _material = Instantiate(Material);
            _data = new float[Points * CategoryColors.Length];
            _currentData = new float[CategoryColors.Length];
            _currentCounts = new int[CategoryColors.Length];
            _currentModes = new GraphAggregationMode[CategoryColors.Length];
            _dirty = true;
        }

        protected virtual void OnValidate()
        {
            if (Renderer == null)
            {
                Renderer = GetComponent<Graphic>();
            }
        }

        protected virtual void Update()
        {
            for (int i = 0; i < CategoryColors.Length; i++)
            {
                CollectData(i, out var value, out var mode);
                // we probably don't need negative values, so lets skip supporting it
                if (value < 0)
                {
                    Debug.LogWarning("Graphing negative values is not supported.");
                    value = 0;
                }
                if (mode != _currentModes[i])
                {
                    _currentModes[i] = mode;
                    ResetCurrent(i);

                }
                switch (mode)
                {
                    case GraphAggregationMode.Average:
                    case GraphAggregationMode.Sum:
                    case GraphAggregationMode.PerSecond:
                        _currentData[i] += value;
                        _currentCounts[i]++;
                        break;
                    case GraphAggregationMode.Min:
                        if (_currentData[i] > value)
                        {
                            _currentData[i] = value;
                        }
                        break;
                    case GraphAggregationMode.Max:
                        if (value > _currentData[i])
                        {
                            _currentData[i] = value;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            _pointTime += Time.deltaTime;
            if (_pointTime > SecondsPerPoint)
            {
                _dataStart = (_dataStart + 1) % Points;
                ClearDataAt(DataLastIndex);
                for (int i = 0; i < CategoryColors.Length; i++)
                {
                    float value = _currentData[i];
                    switch (_currentModes[i])
                    {
                        case GraphAggregationMode.Sum:
                        case GraphAggregationMode.Min:
                        case GraphAggregationMode.Max:
                            // do nothing!
                            break;
                        case GraphAggregationMode.Average:
                            value /= _currentCounts[i];
                            break;
                        case GraphAggregationMode.PerSecond:
                            value /= _pointTime;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    SetCurrentGraphData(i, value);
                    ResetCurrent(i);
                }
                _pointTime = 0;
            }
        }
        private void ResetCurrent(int i)
        {
            switch (_currentModes[i])
            {
                case GraphAggregationMode.Min:
                    _currentData[i] = float.MaxValue;
                    break;
                default:
                    _currentData[i] = 0;
                    break;
            }
            _currentCounts[i] = 0;
        }

        protected virtual string FormatValue(float value)
        {
            return $"{value:N1}";
        }

        protected abstract void CollectData(int category, out float value, out GraphAggregationMode mode);

        private void SetCurrentGraphData(int c, float value)
        {
            _data[DataLastIndex * CategoryColors.Length + c] = value;
            _dirty = true;
        }

        private void ClearDataAt(int i)
        {
            for (int c = 0; c < CategoryColors.Length; c++)
            {
                _data[i * CategoryColors.Length + c] = 0;
            }
            _dirty = true;
        }

        public void LateUpdate()
        {
            if (_dirty)
            {
                _material.SetInt(Width, Points);
                _material.SetInt(DataStart, _dataStart);
                float max = 1;
                if (IsStacked)
                {
                    for (int x = 0; x < Points; x++)
                    {
                        float total = 0;
                        for (int c = 0; c < CategoryColors.Length; c++)
                        {
                            total += _data[x * CategoryColors.Length + c];
                        }
                        if (total > max)
                        {
                            max = total;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < _data.Length; i++)
                    {
                        float v = _data[i];
                        if (v > max)
                        {
                            max = v;
                        }
                    }
                }
                max = AdjustMaxValue(max);
                for (int i = 0; i < LegendTexts.Length; i++)
                {
                    Text legendText = LegendTexts[i];
                    float pct = (float)i / (LegendTexts.Length - 1);
                    legendText.text = FormatValue(max * pct);
                }
                _material.SetFloat(MaxValue, max);
                _material.SetFloatArray(GraphData, _data);
                _material.SetInt(CategoryCount, CategoryColors.Length);
                _material.SetColorArray(Colors, CategoryColors);
                _dirty = false;
            }
        }

        protected virtual float AdjustMaxValue(float max)
        {
            return Mathf.Ceil(max);
        }
    }
}
