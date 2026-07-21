using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProphecyCentury.UI
{
    /// <summary>
    /// Logs the complete UI input path while diagnosing title-screen clicks.
    /// </summary>
    public sealed class RuntimeUiClickDiagnostics : MonoBehaviour
    {
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            var current = EventSystem.current;
            if (current == null)
            {
                Debug.LogWarning($"[UI Click] MouseDown position={Input.mousePosition}; EventSystem.current is null.");
                return;
            }

            _raycastResults.Clear();
            var pointer = new PointerEventData(current)
            {
                position = Input.mousePosition
            };
            current.RaycastAll(pointer, _raycastResults);

            var hits = new StringBuilder();
            for (var i = 0; i < _raycastResults.Count; i += 1)
            {
                if (i > 0)
                {
                    hits.Append(" | ");
                }

                hits.Append(GetHierarchyPath(_raycastResults[i].gameObject));
            }

            var module = current.currentInputModule;
            Debug.Log(
                $"[UI Click] MouseDown position={Input.mousePosition}; " +
                $"EventSystem={current.name}; module={(module != null ? module.GetType().Name : "null")}; " +
                $"hits={_raycastResults.Count}; targets=[{hits}]");
        }

        internal static string GetHierarchyPath(GameObject target)
        {
            if (target == null)
            {
                return "null";
            }

            var path = target.name;
            var parent = target.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }

    public sealed class RuntimeButtonClickLogger : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log(
                $"[UI Button] PointerDown button={RuntimeUiClickDiagnostics.GetHierarchyPath(gameObject)}; " +
                $"position={eventData.position}; interactable={IsInteractable()}");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log(
                $"[UI Button] PointerClick button={RuntimeUiClickDiagnostics.GetHierarchyPath(gameObject)}; " +
                $"position={eventData.position}; interactable={IsInteractable()}");
        }

        private bool IsInteractable()
        {
            var button = GetComponent<UnityEngine.UI.Button>();
            return button != null && button.IsInteractable();
        }
    }
}
