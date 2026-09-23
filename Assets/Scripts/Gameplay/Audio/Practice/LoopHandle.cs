using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// Draggable A or B handle on the zoomed loop editor. Snaps to bars and never crosses the other edge.
    /// </summary>
    public class LoopHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Edge edge;
        [SerializeField] private PracticeZoomView view;
        [SerializeField] private PracticeMenu menu;

        public enum Edge
        {
            From,
            To,
        }

        public void OnBeginDrag(PointerEventData eventData) => Move(eventData);

        public void OnDrag(PointerEventData eventData) => Move(eventData);

        public void OnEndDrag(PointerEventData eventData) => Move(eventData);

        private void Move(PointerEventData eventData)
        {
            menu.DragLoopEdge(edge == Edge.From, view.AudioTimingAt(eventData.position));
        }
    }
}
