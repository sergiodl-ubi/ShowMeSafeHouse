using UnityEngine;
using System.Collections;
using TMPro;

public class NewText : MonoBehaviour
{

    Camera activeCamera;
    BoxCollider localCollider;
    private TextMeshPro textObj;
    private bool colliderResized;
    public bool clicked = false;
    private int TEXT_PADDING_X = 1;
    private int TEXT_PADDING_Y = 1;
    private string originalText = "";
    private string template = "<b><color=\"blue\">[{0}]</color> {1}</b>";

    // Use this for initialization
    void Start()
    {
        activeCamera = Camera.main;
        textObj = GetComponent<TextMeshPro>();
        localCollider = GetComponent<BoxCollider>();
        colliderResized = false;
        originalText = textObj.text;
        textObj.text = string.Format(template, " ", originalText);
    }

    // Update is called once per frame
    void LateUpdate()
    {
        transform.LookAt(activeCamera.transform);
        transform.rotation = Quaternion.LookRotation(activeCamera.transform.forward);
    }

    public void Update()
    {
        if (!colliderResized) ResizeCollider();
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            RaycastHit hit;
            var touchPos = Input.GetTouch(0).position;
            var ray = activeCamera.ScreenPointToRay(touchPos);
            //Debug.Log($"Touch at {touchPos}");
            //Debug.Log($"Ray casted: {ray}");

            if (localCollider.Raycast(ray, out hit, 100.0f))
            {
                clicked = !clicked;
                textObj.text = string.Format(template, clicked ? "x": " ", originalText);
            }
        }
    }

    void ResizeCollider()
    {
        var r = GetComponent<MeshRenderer>();
        Debug.Log($"\n\n\nMeshRenderer: localBounds {r.localBounds} | size {r.localBounds.size}");

        var size = new Vector3(
            r.localBounds.size.x + (TEXT_PADDING_X * 2),
            r.localBounds.size.y + (TEXT_PADDING_Y * 2),
            1);
        localCollider.center = new Vector3(size.x / 2, size.y / -2, transform.position.z); // inverted y axis & begins at top-left corner of mesh
        localCollider.size = size;
        Debug.Log($"Box Collider resized, center at {localCollider.center}, size {localCollider.size}\n\n\n");
        colliderResized = true;
    }
}
