using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum FeelingState
{
    Fear,
    Worried,
    Relax
}

public class StatusIndicator : MonoBehaviour
{
    public Sprite m_Fear;
    public Sprite m_Worried;
    public Sprite m_Relax;
    public float Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            SetFeelingState(ProgressToFeelingState(_progress));
        }
    }
    private float _progress = 0;
    private List<Sprite> spritesList = new List<Sprite>();
    private Image localImage;

    // Start is called before the first frame update
    void Start()
    {
        spritesList.Add(m_Fear);
        spritesList.Add(m_Worried);
        spritesList.Add(m_Relax);
        localImage = GetComponent<Image>();
        localImage.sprite = spritesList[0];
    }

    public void SetFeelingState(FeelingState state)
    {
        localImage.sprite = spritesList[(int)state];
    }

    public FeelingState ProgressToFeelingState(float progress)
    {
        FeelingState state = FeelingState.Fear;
        /*
        var step = 1.0f / spritesList.Count;
        var lowBound = 0.0f;
        var topBound = 0.0f;
        for (var i = 0; i < spritesList.Count; i++)
        {
            topBound = lowBound + step;
            if (progress > lowBound && progress <= topBound) state = (FeelingState)i;
            else if (progress < 0.0f) state = FeelingState.Fear;
            else if (progress > 1.0f) state = FeelingState.Relax;
            lowBound = topBound;
        }
        */
        if (progress >= 0.0f && progress <= 0.4f) state = FeelingState.Fear;
        else if (progress > 0.4f && progress <= 0.8f) state = FeelingState.Worried;
        else if (progress > 0.8f) state = FeelingState.Relax;
        else if (progress < 0.0f) state = FeelingState.Fear;
        Debug.Log($"Progress {progress} converted to {state.ToString()}");
        return state;
    }

    public void Reset() => Progress = 0;
}
