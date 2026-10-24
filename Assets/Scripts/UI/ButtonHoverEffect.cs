using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float pressedScale = 0.97f;
    [SerializeField] private float speed = 12f;

    private Vector3 normalScale;
    private float targetScale = 1f;

    private void Awake()
    {
        normalScale = transform.localScale;
    }

    private void Update()
    {
        Vector3 target = normalScale * targetScale;
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            target,
            Time.unscaledDeltaTime * speed
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = 1f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = hoverScale;
    }
}