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
    [SerializeField] private float deadZone = 0.12f;
    
    private Vector2 inputVector;
    private Vector2 joystickPosition;
    private Canvas canvas;
    
    private void Start()
    {
        if (background == null) background = GetComponent<RectTransform>();
        if (handle == null && transform.childCount > 0) handle = transform.GetChild(0).GetComponent<RectTransform>();
        
        canvas = GetComponentInParent<Canvas>();
        if (background != null)
        {
            joystickPosition = background.position;
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!fixedPosition && background != null)
        {
            background.position = eventData.position;
            joystickPosition = eventData.position;
        }
        OnDrag(eventData);
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (background == null || handle == null) return;

        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, cam, out Vector2 localPoint))
        {
            // Adjust localPoint to be relative to the exact center of background rect, regardless of RectTransform pivot!
            Vector2 centerOffset = new Vector2(
                (0.5f - background.pivot.x) * background.rect.width,
                (0.5f - background.pivot.y) * background.rect.height
            );
            Vector2 pointFromCenter = localPoint - centerOffset;

            Vector2 radius = background.rect.size / 2f;
            if (radius.x > 0 && radius.y > 0)
            {
                inputVector = new Vector2(pointFromCenter.x / radius.x, pointFromCenter.y / radius.y);
            }
            else if (handleRange > 0)
            {
                inputVector = pointFromCenter / handleRange;
            }
            else
            {
                inputVector = Vector2.zero;
            }

            // Apply deadzone to prevent accidental auto-rotation on initial touch
            float mag = inputVector.magnitude;
            if (mag < deadZone)
            {
                inputVector = Vector2.zero;
                handle.anchoredPosition = Vector2.zero;
            }
            else
            {
                if (mag > 1f)
                {
                    inputVector = inputVector.normalized;
                }
                handle.anchoredPosition = inputVector * handleRange;
            }
        }
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        
        if (!fixedPosition && background != null)
        {
            background.position = joystickPosition;
        }
    }
    
    public float Horizontal => inputVector.x;
    public float Vertical => inputVector.y;
    public Vector2 Direction => inputVector;
}
