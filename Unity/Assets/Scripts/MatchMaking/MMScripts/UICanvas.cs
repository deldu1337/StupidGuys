using System;
using UnityEngine;

public class UICanvas : MonoBehaviour
{
    [SerializeField] private GameObject _canvas;
    

    public void onClick()
    {
        if (_canvas != null)
        {
            
            if (!_canvas.activeSelf)
            {
                _canvas.SetActive(true);
            }
            else
            {
                _canvas.SetActive(false);
            }
        }
        
    }
}
