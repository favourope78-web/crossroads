using UnityEngine;

namespace Crossroads.UI
{
    /// <summary>Keeps a full-stretch RectTransform inside Screen.safeArea (notches, rounded corners).</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _last;

        private void Awake()
        {
            _rect = transform as RectTransform;
            Apply();
        }

        private void Update()
        {
            Rect safe = Screen.safeArea;
            if (safe != _last) Apply();
        }

        public void Apply()
        {
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect == null) return;

            Rect safe = Screen.safeArea;
            _last = safe;
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
