using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class MobileButtonHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public UnityEvent OnPointerDownEvent = new UnityEvent();
    public UnityEvent OnPointerUpEvent = new UnityEvent();

    public void OnPointerDown(PointerEventData eventData)
    {
        OnPointerDownEvent?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        OnPointerUpEvent?.Invoke();
    }
}
