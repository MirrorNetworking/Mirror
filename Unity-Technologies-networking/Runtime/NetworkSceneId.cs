#if ENABLE_UNET
using System;

namespace UnityEngine.Networking
{
    [Serializable]
    public struct NetworkSceneId
    {
        public NetworkSceneId(uint value)
        {
            m_Value = value;
        }

        [SerializeField]
        uint m_Value;

        public bool IsEmpty()
        {
            return m_Value == 0;
        }

        public override int GetHashCode()
        {
            return (int)m_Value;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkSceneId && this == (NetworkSceneId)obj;
        }

        public static bool operator==(NetworkSceneId c1, NetworkSceneId c2)
        {
            return c1.m_Value == c2.m_Value;
        }

        public static bool operator!=(NetworkSceneId c1, NetworkSceneId c2)
        {
            return c1.m_Value != c2.m_Value;
        }

        public override string ToString()
        {
            return m_Value.ToString();
        }

        public uint Value { get { return m_Value; } }
    }
}
#endif
