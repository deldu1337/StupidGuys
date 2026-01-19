using UnityEngine;
using UnityEngine.UI;

public class ScrollingBackground : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 0.2f;

    private RawImage _rawImage;

    private void Start()
    {
        _rawImage = GetComponent<RawImage>();
    }

    private void Update()
    {
        if (_rawImage != null)
        {
            Rect uvRect = _rawImage.uvRect;
            uvRect.y -= scrollSpeed * Time.deltaTime;
            _rawImage.uvRect = uvRect;
        }
    }
}