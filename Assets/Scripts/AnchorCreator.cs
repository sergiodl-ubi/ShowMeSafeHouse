using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

public class AnchorCreator : MonoBehaviour
{
    [SerializeField]
    GameObject m_Prefab;

    public GameObject prefab
    {
        get => m_Prefab;
        set => m_Prefab = value;
    }

    public IDictionary<ARAnchor, BoundingBox> Anchors { get => anchorDic; }

    public void RemoveAllAnchors()
    {
        Debug.Log($"DEBUG: Removing all anchors ({anchorDic.Count})");
        foreach (var anchor in anchorDic)
        {
            Destroy(anchor.Key.gameObject);
        }
        s_Hits.Clear();
        anchorDic.Clear();
        var sIndicator = GameObject.Find("Status Indicator");
        var statusIndicator = sIndicator.GetComponent<StatusIndicator>();
        statusIndicator.Reset();
    }

    void Awake()
    {
        m_RaycastManager = GetComponent<ARRaycastManager>();
        m_AnchorManager = GetComponent<ARAnchorManager>();
        var cameraImage = GameObject.Find("Camera Image");
        phoneARCamera = cameraImage.GetComponent<PhoneARCamera>();
    }

    ARAnchor CreateAnchor(in ARRaycastHit hit)
    {
        ARAnchor anchor = null;
        // TODO: create plane anchor

        // create a regular anchor at the hit pose
        Debug.Log($"DEBUG: Creating regular anchor. distance: {hit.distance}. session distance: {hit.sessionRelativeDistance} type: {hit.hitType}.");

        var gameObject = Instantiate(prefab, hit.pose.position, hit.pose.rotation);
        anchor = gameObject.GetComponent<ARAnchor>();
        if (anchor == null)
        {
            anchor = gameObject.AddComponent<ARAnchor>();
        }
        return anchor;
    }

    private bool Pos2Anchor(float x, float y, BoundingBox outline)
    {
        // Perform the raycast
        if (m_RaycastManager.Raycast(new Vector2(x, y), s_Hits, trackableTypes))
        {
            // Raycast hits are sorted by distance, so the first one will be the closest hit.
            var hit = s_Hits[0];
            // Create a new anchor
            Debug.Log("Creating Anchor");
            var anchor = CreateAnchor(hit);
            if (anchor)
            {
                Debug.Log($"DEBUG: creating anchor. {outline}");
                // Remember the anchor so we can remove it later.
                anchorDic.Add(anchor, outline);
                Debug.Log($"DEBUG: Current number of anchors {anchorDic.Count}.");
                var textObj = anchor.GetComponent<TMPro.TextMeshPro>();
                textObj.text = $"{outline.Label}|{(int)(outline.Confidence * 100)}";
                return true;
            }
            else
            {
                Debug.Log("DEBUG: Error creating anchor");
                return false;
            }
        }
        return false;
    }

    void Update()
    {
        // If bounding boxes are not stable, return directly without raycast
        if (!phoneARCamera.recognitionFinished)
        {
            return;
        }

        boxSavedOutlines = phoneARCamera.boxSavedOutlines;
        shiftX = phoneARCamera.shiftX;
        shiftY = phoneARCamera.shiftY;
        scaleFactor = phoneARCamera.scaleFactor;
        // Remove outdated anchor that is not in boxSavedOutlines
        // Currently not using. Can be removed.
        if (anchorDic.Count != 0)
        {
            List<ARAnchor> itemsToRemove = new List<ARAnchor>();
            foreach ((ARAnchor anchor, BoundingBox box) in anchorDic)
            {
                if (!boxSavedOutlines.ContainsKey(box.BoxId))
                {
                    Debug.Log($"DEBUG: anchor ({box.BoxId}) removed. {box.Label}: {(int)(box.Confidence * 100)}%");

                    itemsToRemove.Add(anchor);
                    // m_AnchorManager.RemoveAnchor(pair.Key);
                    Destroy(anchor);
                    s_Hits.Clear();
                }
            }
            foreach (var anchor in itemsToRemove)
            {
                anchorDic.Remove(anchor);
            }
        }

        // return if no bounding boxes
        if (boxSavedOutlines.Count == 0)
        {
            return;
        }
        // create anchor for new bounding boxes
        foreach (BoundingBox outline in boxSavedOutlines.Values)
        {
            if (outline.Used)
            {
                continue;
            }

            // var xMin = outline.Dimensions.X * this.scaleFactor + this.shiftX;
            // var width = outline.Dimensions.Width * this.scaleFactor;
            // var yMin = outline.Dimensions.Y * this.scaleFactor + this.shiftY;
            // var height = outline.Dimensions.Height * this.scaleFactor;
            (float xMin, float yMin, float width, float height) = phoneARCamera.scaledBBToScreenDims(outline.Dimensions);
            // Note: rect bounding box coordinates starts from top left corner.
            // AR camera starts from borrom left corner.
            // Need to flip Y axis coordinate of the anchor 2D position when raycast
            yMin = Screen.height - yMin;

            float center_x = xMin + (width / 2f);
            float center_y = yMin - (height / 2f);

            if (Pos2Anchor(center_x, center_y, outline))
            {
                Debug.Log("Outline used is true");
                outline.Used = true;
            }
            else
            {
                //Debug.Log("Outline used is false");
            }
        }

    }

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    IDictionary<ARAnchor, BoundingBox> anchorDic = new Dictionary<ARAnchor, BoundingBox>();

    // from PhoneARCamera
    private Dictionary<int, BoundingBox> boxSavedOutlines;
    private float shiftX;
    private float shiftY;
    private float scaleFactor;

    public PhoneARCamera phoneARCamera;
    public ARRaycastManager m_RaycastManager;
    public ARAnchorManager m_AnchorManager;

    // Raycast against planes and feature points
    //const TrackableType trackableTypes = TrackableType.Planes;//FeaturePoint;
    const TrackableType trackableTypes = TrackableType.Planes | TrackableType.FeaturePoint;
}
