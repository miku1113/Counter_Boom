using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick Components")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    
    [Header("Settings")]
    [SerializeField] private float handleRange = 50f;
    [SerializeField] private bool fixedPosition = true;
    
    private Vector2 inputVector;
    private Vector2 joystickPosition;
    private Canvas canvas;
    
    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        joystickPosition = background.position;
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!fixedPosition)
        {
            background.position = eventData.position;
            joystickPosition = eventData.position;
        }
        OnDrag(eventData);
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        Vector2 position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, background.position);
        Vector2 radius = background.sizeDelta / 2;
        
        inputVector = (eventData.position - position) / (radius * canvas.scaleFactor);
        
        // Clamp to circle
        if (inputVector.magnitude > 1f)
        {
            inputVector = inputVector.normalized;
        }
        
        // Move handle
        handle.anchoredPosition = inputVector * handleRange;
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
        
        if (!fixedPosition)
        {
            background.position = joystickPosition;
        }
    }
    
    public float Horizontal => inputVector.x;
    public float Vertical => inputVector.y;
    public Vector2 Direction => inputVector;
}
