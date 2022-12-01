using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CubeCounter : MonoBehaviour
{
    private int _count = 0;
    public int Count
    {
        get => _count;
        set
        {
            _count = value;
            SetTMProText();
        }
    }

    void Start()
    {
        Count = 0;
    }

    private void SetTMProText()
    {
        var textObj = GetComponent<TextMeshProUGUI>();
        textObj.text = $"<b>Visible Cubes: <color=green>{_count}</b>";
    }
}
