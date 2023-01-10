using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Linq;
using TMPro;

public class AnchorCreator : MonoBehaviour
{
    [SerializeField]
    GameObject m_ChecklistPrefab;

    public GameObject checklistPrefab
    {
        get => m_ChecklistPrefab;
        set => m_ChecklistPrefab = value;
    }

    [SerializeField]
    GameObject m_BoundingCubePrefab;
    public GameObject boundingCubePrefab
    {
        get => m_BoundingCubePrefab;
        set => m_BoundingCubePrefab = value;
    }

    public IDictionary<ARAnchor, BoundingBox> Anchors { get => anchorDic; }
    public IDictionary<ARAnchor, BoundingCube> Cubes { get => boundingCubeAnchorsDic; }

    public void RemoveAllAnchors()
    {
        Debug.Log($"DEBUG: Removing all anchors ({anchorDic.Count})");
        foreach (var (anchor, cube) in boundingCubeAnchorsDic)
        {
            if (anchor && anchor.gameObject) Destroy(anchor.gameObject);
            if (cube && cube.gameObject) Destroy(cube.gameObject);
        }
        foreach (var (anchor, bb) in anchorDic)
        {
            if (anchor && anchor.gameObject) Destroy(anchor.gameObject);
        }
        boundingCubeAnchorsDic.Clear();
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

    private ARAnchor CreateAnchor(in ARRaycastHit hit)
    {
        ARAnchor anchor = null;
        // TODO: create plane anchor

        // create a regular anchor at the hit pose
        Debug.Log($"DEBUG: Creating regular anchor. distance: {hit.distance}. session distance: {hit.sessionRelativeDistance} type: {hit.hitType}.");

        var gameObject = Instantiate(checklistPrefab, hit.pose.position, hit.pose.rotation);
        anchor = gameObject.GetComponent<ARAnchor>();
        if (anchor == null)
        {
            anchor = gameObject.AddComponent<ARAnchor>();
        }
        return anchor;
    }

    public void DestroyAnchor(ARAnchor anchor)
    {
        if (boundingCubeAnchorsDic.ContainsKey(anchor)) {
            Debug.Log($"DestroyAnchor {anchor}: destroying cube");
            var cube = boundingCubeAnchorsDic[anchor];
            if (cube && cube.gameObject)
            {
                Destroy(cube.gameObject);
                Debug.Log($"Cube destroyed");
            } 
        }
        boundingCubeAnchorsDic.Remove(anchor);
        anchorDic.Remove(anchor);
        if (anchor && anchor.gameObject)
        {
            Destroy(anchor.gameObject);
            Debug.Log($"Anchor destroyed");
        }
    }

    private ARAnchor AnchorBoundingCube(ARAnchor refAnchor)
    {

        var gameObj = Instantiate(
            boundingCubePrefab,
            refAnchor.transform.position,
            refAnchor.transform.rotation);
        var anchor = gameObj.GetComponent<ARAnchor>();
        if (anchor == null)
        {
            anchor = gameObj.AddComponent<ARAnchor>();
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

                var bcAnchor = AnchorBoundingCube(anchor);
                var cubeObj = bcAnchor.GetComponent<BoundingCube>();
                cubeObj.SetSize(ObjectClasses.BoundingCubeScales[outline.Label]);
                boundingCubeAnchorsDic.Add(anchor, cubeObj);
                Debug.Log($"DEBUG: Current number of bounding boxes {boundingCubeAnchorsDic.Count}.");

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

            (float xMin, float yMin, float width, float height) = SelectSegmentbyFeaturedPoints(outline);
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

    private BoundingBoxDimensions SelectSegmentbyFeaturedPoints(BoundingBox outline)
    {
        var fpToTest = 10; // feature points to test if they're inside the segment
        float xMin, yMin, width, height, cx, cy;
        xMin = yMin = width = height = cx = cy = 0.0f;
        var rect = new Rect(xMin, yMin, width, height);
        Vector3 screenPoint;
        var segments = new Dictionary<int, int>();
        Debug.Log($"Bounding Box of size: {outline.getSize().ToString()}");
        foreach(BoundingBox segment in outline.Segments.Values)
        {
            tmpHits.Clear();
            segments[segment.BoxId] = 0;
            (xMin, yMin, width, height) = phoneARCamera.scaledBBToScreenDims(segment.Dimensions);
            rect.Set(xMin, yMin, width, height);
            yMin = Screen.height - yMin;
            cx = xMin + (width / 2f);
            cy = yMin - (height / 2f);
            if (m_RaycastManager.Raycast(new Vector2(cx, cy), tmpHits, trackableTypes))
            {
                for (var i = 0; i < tmpHits.Count && i < fpToTest; i++)
                {
                    var pos = Camera.main.WorldToScreenPoint(tmpHits[i].pose.position);
                    // ScreenPoint is bottom-left origin, Rect are bottom-top origin, converting.
                    screenPoint = new Vector3(pos.x, Screen.height - pos.y, pos.z);
                    var contains = rect.Contains(screenPoint);
                    // Debug.Log($"Feature point SP:{screenPoint} is inside? {contains.ToString()}");
                    segments[segment.BoxId] += contains ? 1: 0;
                }
            }
            Debug.Log($"Segment {rect} contains {segments[segment.BoxId]} feature points (id: {segment.BoxId})");
        }
        var (boxID, count) = segments.OrderByDescending(segment => segment.Value).ToList().First();
        var prospectArea = phoneARCamera.scaledBBToScreenDims(outline.Segments[boxID].Dimensions);
        Debug.Log($"DEBUG: Segment selected {prospectArea}");
        return prospectArea;
    }

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
    static List<ARRaycastHit> tmpHits = new List<ARRaycastHit>();

    IDictionary<ARAnchor, BoundingBox> anchorDic = new Dictionary<ARAnchor, BoundingBox>();
    IDictionary<ARAnchor, BoundingCube> boundingCubeAnchorsDic = new Dictionary<ARAnchor, BoundingCube>();

    // from PhoneARCamera
    private Dictionary<int, BoundingBox> boxSavedOutlines;

    public PhoneARCamera phoneARCamera;
    public ARRaycastManager m_RaycastManager;
    public ARAnchorManager m_AnchorManager;

    // Raycast against planes and feature points
    //const TrackableType trackableTypes = TrackableType.Planes;//FeaturePoint;
    const TrackableType trackableTypes = TrackableType.Planes | TrackableType.FeaturePoint;
}
