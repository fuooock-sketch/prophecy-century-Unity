using UnityEngine;
using UnityEngine.EventSystems;

namespace ProphecyCentury.UI
{
    public sealed class RuntimeUnitDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RunSceneController Controller { get; set; }
        public string Source { get; set; }
        public int HandIndex { get; set; } = -1;
        public string BoardSlotId { get; set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Controller?.BeginRuntimeDrag(Source, HandIndex, BoardSlotId, eventData, transform as RectTransform);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Controller?.UpdateRuntimeDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Controller?.CompleteRuntimeDrag(eventData);
        }
    }
}
