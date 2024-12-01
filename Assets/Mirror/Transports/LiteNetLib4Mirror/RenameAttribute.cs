#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Mirror.LiteNetLib4Mirror
{
	public class RenameAttribute : PropertyAttribute
	{
		public RenameAttribute(string name)
		{
			NewName = name;
		}

		public string NewName { get; }
	}

	public class ArrayRenameAttribute : PropertyAttribute
	{
		public ArrayRenameAttribute(string name)
		{
			NewName = name;
		}

		public string NewName { get; }
	}

	[CustomPropertyDrawer(typeof(RenameAttribute))]
	public class RenameEditor : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.PropertyField(position, property, new GUIContent(((RenameAttribute)attribute).NewName));
		}
	}

	[CustomPropertyDrawer(typeof(ArrayRenameAttribute))]
	public class ArrayRenameEditor : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.PropertyField(position, property, label.text.EndsWith(" 0") ? new GUIContent(label.text.Replace("Element", ((ArrayRenameAttribute)attribute).NewName) + " (Default)") : new GUIContent(label.text.Replace("Element", ((ArrayRenameAttribute)attribute).NewName)));
		}
	}
}
#endif
