using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ClothDynamics
{
    public static class InspectorLocker
    {
        private static EditorWindow _mouseOverWindow;
        public static bool _unlocked = true;

        [MenuItem("ClothDynamics/Toggle Lock &q")]
        public static void Toggle()
        {
            ReadValue(out PropertyInfo propertyInfo, out bool value);
            if (propertyInfo != null)
            {
                propertyInfo.SetValue(_mouseOverWindow, !value, null);
                _mouseOverWindow.Repaint();
            }
        }

        private static void ReadValue(out PropertyInfo propertyInfo, out bool value)
        {
            propertyInfo = null;
            value = _unlocked;
            if (_mouseOverWindow == null)
            {
                if (!EditorPrefs.HasKey("LockableInspectorIndex"))
                {
                    EditorPrefs.SetInt("LockableInspectorIndex", 0);
                }
                int i = EditorPrefs.GetInt("LockableInspectorIndex");

                var type = Assembly
                    .GetAssembly(typeof(Editor))
                    .GetType("UnityEditor.InspectorWindow");

                var list = Resources.FindObjectsOfTypeAll(type);
                _mouseOverWindow = list.ElementAtOrDefault(i) as EditorWindow;
            }

            if (_mouseOverWindow != null && _mouseOverWindow.GetType().Name == "InspectorWindow")
            {
                var type = Assembly
                .GetAssembly(typeof(Editor))
                .GetType("UnityEditor.InspectorWindow");

                propertyInfo = type.GetProperty("isLocked");
                value = (bool)propertyInfo.GetValue(_mouseOverWindow, null);
                _unlocked = value;
            }
        }

        public static void UpdateLockValue()
        {
            ReadValue(out PropertyInfo propertyInfo, out bool value);
            _unlocked = value;
            if (_mouseOverWindow != null)_mouseOverWindow.Repaint();
        }
    }
}