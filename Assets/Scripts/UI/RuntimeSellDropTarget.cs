using UnityEngine;
using UnityEngine.EventSystems;

namespace ProphecyCentury.UI
{
    public sealed class RuntimeSellDropTarget : MonoBehaviour, IDropHandler
    {
        public RunSceneController Controller { get; set; }

        public void OnDrop(PointerEventData eventData)
        {
            Controller?.DropRuntimeDragOnSellArea();
        }
    }
}
