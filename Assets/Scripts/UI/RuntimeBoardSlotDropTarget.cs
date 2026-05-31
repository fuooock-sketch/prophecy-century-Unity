using UnityEngine;
using UnityEngine.EventSystems;

namespace ProphecyCentury.UI
{
    public sealed class RuntimeBoardSlotDropTarget : MonoBehaviour, IDropHandler
    {
        public RunSceneController Controller { get; set; }
        public string BoardSlotId { get; set; }

        public void OnDrop(PointerEventData eventData)
        {
            Controller?.DropRuntimeDragOnBoardSlot(BoardSlotId);
        }
    }
}
