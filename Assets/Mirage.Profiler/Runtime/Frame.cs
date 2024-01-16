using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirage.NetworkProfiler
{
    [System.Serializable]
    public class Frames : IEnumerable<Frame>
    {
        [SerializeField]
        private Frame[] _frames;

        public Frames()
        {
            _frames = new Frame[NetworkProfilerRecorder.FRAME_COUNT];
            for (var i = 0; i < _frames.Length; i++)
                _frames[i] = new Frame();
        }

        public Frame GetFrame(int frameIndex)
        {
            return _frames[frameIndex % _frames.Length];
        }

        public IEnumerator<Frame> GetEnumerator() => ((IEnumerable<Frame>)_frames).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<Frame>)_frames).GetEnumerator();

        internal void ValidateSize()
        {
            if (_frames.Length != NetworkProfilerRecorder.FRAME_COUNT)
            {
                Array.Resize(ref _frames, NetworkProfilerRecorder.FRAME_COUNT);
            }
        }
    }

    [System.Serializable]
    public class Frame
    {
        public List<MessageInfo> Messages = new List<MessageInfo>();
        public int Bytes;
    }
}
