using System;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
namespace Mirror
{
    public class NetworkUsageGraph : MonoBehaviour
    {
        private static readonly int MaxValue = Shader.PropertyToID("_MaxValue");
        private static readonly int GraphData = Shader.PropertyToID("_GraphData");
        private static readonly int CategoryCount = Shader.PropertyToID("_CategoryCount");
        private static readonly int Colors = Shader.PropertyToID("_CategoryColors");
        private static readonly int Width = Shader.PropertyToID("_Width");
        private static readonly int DataStart = Shader.PropertyToID("_DataStart");

        public Material Material;
        public Graphic Renderer;
        [Range(1, 128)]
        public int Points = 64;
        [Min(1)]
        public int FramesPerPoint = 1;
        public Color[] CategoryColors = new[]
        {
            Color.cyan
        };

        private float[] _data;
        private int _dataStart;
        private bool _dirty;

        private int DataLastIndex => ((_dataStart - 1) + Points) % Points;

        private void Awake()
        {
            Renderer.material = Material;
            _data = new float[Points * CategoryColors.Length];
            _dirty = true;
        }

        private void OnValidate()
        {
            if (Material == null)
            {
                Material = new Material(Shader.Find("Mirror/NetworkGraph"));
            }
            if (Renderer == null)
            {
                Renderer = GetComponent<Graphic>();
            }
        }

        // test draw
        private float _counter;
        private void Update()
        {
            _counter += Time.deltaTime;
            if (_counter > 0.1f)
            {
                _dataStart = (_dataStart + 1) % Points;
                ClearDataAt(DataLastIndex);
                _counter = 0;
            }
            AddCurrentData(0, Random.value * 50);
            AddCurrentData(1, Random.value * 50);
        }
        private void AddCurrentData(int c, float value)
        {
            _data[DataLastIndex * CategoryColors.Length + c] += value;
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
                Material.SetInt(Width, Points);
                Material.SetInt(DataStart, _dataStart);
                float max = 1;
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
                Material.SetFloat(MaxValue, max);
                Material.SetFloatArray(GraphData, _data);
                Material.SetInt(CategoryCount, CategoryColors.Length);
                Material.SetColorArray(Colors, CategoryColors);
                _dirty = false;
            }
        }
    }
}
