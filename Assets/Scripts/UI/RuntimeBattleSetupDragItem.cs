using UnityEngine;
using UnityEngine.EventSystems;

namespace ProphecyCentury.UI
{
    public sealed class RuntimeBattleSetupDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RunSceneController Controller { get; set; }
        public string PositionKey { get; set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Controller?.BeginBattleSetupUnitDrag(PositionKey, transform as RectTransform);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Controller?.DragBattleSetupUnit(PositionKey, transform as RectTransform, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Controller?.EndBattleSetupUnitDrag(PositionKey, transform as RectTransform);
        }
    }
}
