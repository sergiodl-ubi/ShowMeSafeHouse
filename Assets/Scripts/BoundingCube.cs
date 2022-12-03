using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class BoundingCube : MonoBehaviour
{
    public bool InitialVisibility;
    private bool _visible = false;
    public bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            m_MeshRenderer.sharedMaterial.color = _visible ? debugColor : invisibleColor;
        }
    }
    public bool IsOnCamera { get => m_Renderer.isVisible; }
    private Renderer m_Renderer;
    private MeshRenderer m_MeshRenderer;
    private Color debugColor;
    private Color invisibleColor;

    // Start is called before the first frame update
    void Start()
    {
        m_Renderer = this.GetComponent<Renderer>();
        m_MeshRenderer = this.GetComponent<MeshRenderer>();
        var orig = m_MeshRenderer.sharedMaterial.color;
        debugColor = new Color(orig.r, orig.g, orig.b, orig.a);
        invisibleColor = new Color(orig.r, orig.g, orig.b, 0f);
        Visible = InitialVisibility;
    }

    void OnDestroy()
    {
        Destroy(GetComponent<ARAnchor>());
    }

    public void SetSize(Vector3 scale)
    {
        transform.localScale += scale;
    }
}
