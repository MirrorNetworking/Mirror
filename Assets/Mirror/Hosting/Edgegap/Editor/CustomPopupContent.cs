#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Edgegap.Editor
{
    public class CustomPopupContent : PopupWindowContent
    {
        private Vector2 scrollPos;
        private List<string> _btnNames;
        private Action<string> _onBtnClick;
        private string _defaultValue = "";

        private float _minHeight = 25;
        private float _maxHeight = 100;
        private float _width;

        public CustomPopupContent(
            List<string> btnNames,
            Action<string> btnCallback,
            string defaultValue,
            float width = 400
        )
        {
            _btnNames = btnNames;
            _onBtnClick = btnCallback;
            _width = width;
            _defaultValue = defaultValue;
        }

        public override Vector2 GetWindowSize()
        {
            float height = _minHeight;

            if (_btnNames.Count > 0)
            {
                height *= _btnNames.Count;
            }

            return new Vector2(_width, height <= _maxHeight ? height : _maxHeight);
        }

        public override void OnGUI(Rect rect)
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            foreach (string name in _btnNames)
            {
                if (GUILayout.Button(name, GUILayout.Width(_width - 25)))
                {
                    if (name == "Create New Application")
                    {
                        _onBtnClick(_defaultValue);
                    }
                    else
                    {
                        _onBtnClick(name);
                    }

                    editorWindow.Close();
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
