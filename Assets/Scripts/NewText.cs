using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

public class ChecklistLine
{
    public ChecklistLine(string text, bool marked = false, bool isRawText = false)
    {
        _isRawText = isRawText;
        _checked = marked;
        Text = text;
    }
    public bool IsRawText { get => _isRawText; }
    public bool Checked
    {
        get => _checked;
        set
        {
            _checked = value;
            Text = _rawText;
        }
    }
    public string Text
    {
        get => _text;
        set
        {
            _rawText = value;
            _text = IsRawText ?
                _rawText :
                string.Format(lineTemplate, Checked ? "x" : " ", _rawText);
        }
    }
    public void Toggle() => Checked = !_checked;
    private string lineTemplate = "<b>[<color=\"red\">{0}</color>] {1}</b>";
    private bool _isRawText = false;
    private string _rawText = "";
    private string _text = "";
    private bool _checked = false;
}

public class NewText : MonoBehaviour
{
    /// <summary>
    /// Lines of the text block, first element is the text, second whether the checkbox should be ticket or not
    /// <summary>

    public List<ChecklistLine> Lines = new List<ChecklistLine>();
    public string Label;
    public string Confidence;
    public int CheckboxCount { get => _checkboxCount; }
    public int CheckboxesActive { get => _checkboxesActive; }
    public Material BackgroundMaterial;
    public float PaddingTop = 0;

    public float PaddingBottom = 0;

    public float PaddingLeft = 0;

    public float PaddingRight = 0;

    void Awake()
    {
        var arOrigin = GameObject.Find("AR Session Origin");
        anchorCreator = arOrigin.GetComponent<AnchorCreator>();
        var sIndicator = GameObject.Find("Status Indicator");
        statusIndicator = sIndicator.GetComponent<StatusIndicator>();
    }

    // Use this for initialization
    void Start()
    {
        activeCamera = Camera.main;
        textObj = GetComponent<TextMeshPro>();
        localCollider = GetComponent<BoxCollider>();
        colliderResized = false;
        var data = textObj.text.Split('|');
        if (data.Length != 2)
        {
            Debug.LogException(
                new ArgumentException(
                    "Assigned name is not properly formatted as \"label|confidence\"",
                    textObj.text),
                this
            );
        }
        Label = data[0];
        Confidence = data[1];
        // Text resize will affect touch localization. Fix later
        textObj.text = $"<b><size=80%>{Label}</size></b>";
        Lines.Add(new ChecklistLine(textObj.text, isRawText: true));
        var classesGuides = ObjectClasses.Guides;
        if (classesGuides.ContainsKey(Label))
        {
            var guides = classesGuides[Label];
            _checkboxCount = guides.Count;
            for (var i = 0; i < _checkboxCount; i++)
            {
                var guide = new ChecklistLine(guides[i]);
                textObj.text += "<br>" + guide.Text;
                Lines.Add(guide);
            }
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        transform.LookAt(activeCamera.transform);
        transform.rotation = Quaternion.LookRotation(activeCamera.transform.forward);
    }

    void Update()
    {
        if (!colliderResized)
        {
            ResizeCollider();
            ResizeBackground();
        }
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            RaycastHit hit;
            var touchPos = Input.GetTouch(0).position;
            var ray = activeCamera.ScreenPointToRay(touchPos);
            //Debug.Log($"Touch at {touchPos}");
            //Debug.Log($"Ray casted: {ray}");

            if (localCollider.Raycast(ray, out hit, 100.0f))
            {
                //textObj.text = string.Format(template, clicked ? "x" : " ", originalText);
                Debug.Log($"\n\nCollider hit registered at Point.Y-{hit.point.y} LocalPos.Y-{hit.transform.localPosition.y} ScaleY-{hit.transform.localScale.y}");
                var lineTouched = lineNumberTouched(hit);
                if (lineTouched > 0)
                {
                    Lines[lineTouched].Toggle();
                    recountActiveCheckboxes();
                    reloadText();
                    updateStatusIndicator();
                }
            }
        }
    }

    void OnDestroy()
    {
        Destroy(GetComponent<ARAnchor>());
    }

    private void ResizeCollider()
    {
        var r = GetComponent<MeshRenderer>();
        Debug.Log($"\nMeshRenderer: localBounds {r.localBounds} | size {r.localBounds.size}");

        var size = new Vector3(
            r.localBounds.size.x + (TEXT_PADDING_X * 2),
            r.localBounds.size.y + (TEXT_PADDING_Y * 2),
            0.01f);
        localCollider.center = new Vector3(size.x / 2, size.y / -2, transform.position.z); // inverted y axis & begins at top-left corner of mesh
        localCollider.size = size;
        Debug.Log($"Box Collider resized, center at {localCollider.center}, size {localCollider.size}\n");
        colliderResized = true;
    }

    private int lineNumberTouched(RaycastHit hit)
    {
        var meshSize = GetComponent<MeshRenderer>().localBounds.size;
        var lineHeight = meshSize.y / Lines.Count;
        var hitOnY = ((hit.point.y - hit.transform.localPosition.y) * -1) / hit.transform.localScale.y;
        Debug.Log($"MeshSizeY: {meshSize.y} | lineHeight {lineHeight} | Hit Y {hitOnY}");
        var lineIdxHit = -1;
        for (var lineIdx = 0; lineIdx < Lines.Count; lineIdx++)
        {
            var minY = lineIdx * lineHeight;
            var maxY = minY + lineHeight;
            // Debug.Log($"MinY {minY} MaxY {maxY}");
            if (hitOnY > minY && hitOnY <= maxY)
            {
                Debug.Log($"LineIdx {lineIdx} was hit.\n");
                lineIdxHit = lineIdx;
                break;
            }
        }
        return lineIdxHit;
    }

    private void reloadText()
    {
        // setting title
        textObj.text = "";
        for (var i = 0; i < Lines.Count; i++)
        {
            textObj.text += (i > 0 ? "<br>" : "") + Lines[i].Text;
        }
    }

    private void recountActiveCheckboxes()
    {
        _checkboxesActive = 0;
        for (var i = 0; i < Lines.Count; i++)
        {
            if (!Lines[i].IsRawText && Lines[i].Checked) _checkboxesActive++;
        }
        // Debug.Log($"{Label}@{Confidence} active boxes: {CheckboxesActive}/{CheckboxCount}");
    }

    private void updateStatusIndicator()
    {
        var anchors = anchorCreator.Anchors;
        var checkboxCount = 0;
        var activeCheckboxCount = 0;
        foreach (ARAnchor anchor in anchors.Keys)
        {
            var tmp = anchor.GetComponent<NewText>();
            checkboxCount += tmp.CheckboxCount;
            activeCheckboxCount += tmp.CheckboxesActive;
        }
        float progress = (float)activeCheckboxCount / (float)checkboxCount;
        Debug.Log($"Overall status: {activeCheckboxCount}/{checkboxCount} | Progress {progress}");
        statusIndicator.Progress = progress;
    }

    private void ResizeBackground()
    {
        bgTransform = transform.Find("Background");
        var bounds = textObj.bounds;
        // Debug.Log($"{bounds}");

        var pos = bounds.center;
        var hoseiX = -(PaddingLeft / 2) + (PaddingRight / 2);
        var hoseiY = -(PaddingBottom / 2) + (PaddingTop / 2);
        var hoseiZ = 0.05f;
        bgTransform.localPosition = new Vector3(pos.x + hoseiX, pos.y + hoseiY, pos.z + hoseiZ);

        var scale = bounds.extents;
        var hoseiW = (PaddingLeft + PaddingRight) / 10;
        var hoseiH = (PaddingTop + PaddingBottom) / 10;
        bgTransform.localScale = new Vector3((scale.x / 10 * 2) + hoseiW, 1, (scale.y / 10 * 2) + hoseiH);
    }

    Camera activeCamera;
    BoxCollider localCollider;
    Transform bgTransform;
    AnchorCreator anchorCreator;
    StatusIndicator statusIndicator;
    private int _checkboxCount = 0;
    private int _checkboxesActive = 0;
    private TextMeshPro textObj;
    private bool colliderResized;
    private int TEXT_PADDING_X = 0;
    private int TEXT_PADDING_Y = 0;
}
