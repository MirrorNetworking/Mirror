using System;
using System.Collections.Generic;
using UnityEngine;


namespace Mirror.Profiler.Table
{

    [Serializable]
    public abstract class TreeElement
    {
        [SerializeField] int m_ID;
        [SerializeField] int m_Depth;
        [NonSerialized] TreeElement m_Parent;
        [NonSerialized] List<TreeElement> m_Children;

        public int depth
        {
            get { return m_Depth; }
            set { m_Depth = value; }
        }

        public TreeElement parent
        {
            get { return m_Parent; }
            set { m_Parent = value; }
        }

        public abstract string name
        {
            get;
        }

        public List<TreeElement> children
        {
            get { return m_Children; }
            set { m_Children = value; }
        }

        public bool hasChildren
        {
            get { return children != null && children.Count > 0; }
        }

        public int id
        {
            get { return m_ID; }
            set { m_ID = value; }
        }

        public TreeElement()
        {
        }

        public TreeElement( int depth, int id)
        {
            m_ID = id;
            m_Depth = depth;
        }
    }

}


