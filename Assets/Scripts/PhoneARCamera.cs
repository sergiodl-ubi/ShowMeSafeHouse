using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Barracuda;

using System.IO;
using TFClassify;
using System.Linq;
using System.Collections;

using Stopwatch = System.Diagnostics.Stopwatch;

public class PhoneARCamera : MonoBehaviour
{
    [SerializeField]
    ARCameraManager m_CameraManager;

    /// <summary>
    /// Get or set the <c>ARCameraManager</c>.
    /// </summary>
    public ARCameraManager cameraManager
    {
        get => m_CameraManager;
        set => m_CameraManager = value;
    }

    [SerializeField]
    RawImage m_RawImage;

    /// <summary>
    /// The UI RawImage used to display the image on screen. (deprecated)
    /// </summary>
    public RawImage rawImage
    {
        get { return m_RawImage; }
        set { m_RawImage = value; }
    }

    public enum Detectors
    {
        Yolo2_tiny,
        Yolo3_tiny
    };
    public Detectors selected_detector;

    public Detector detector = null;

    public float shiftX = 0f;
    public float shiftY = 0f;
    public float scaleFactor = 1;

    public Color colorTag = new Color(0.3843137f, 0, 0.9333333f);
    private static GUIStyle labelStyle;
    private static Texture2D boxOutlineTexture;
    // bounding boxes detected for current frame
    private IList<BoundingBox> boxOutlines = new List<BoundingBox>();
    // bounding boxes detected across frames
    public List<BoundingBox> boxSavedOutlines = new List<BoundingBox>();
    // lock model when its inferencing a frame
    private bool isDetecting = false;

    // the number of frames that bounding boxes stay static
    private int stabilityCounter = 0;
    public bool localization = false;
    private int inferenceCounter = 0;
    private int rawImageCounter = 0;
    private int groupBoxingCounter = 0;

    Texture2D m_Texture;

    void Awake()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        boxOutlineTexture = new Texture2D(1, 1);
        boxOutlineTexture.SetPixel(0, 0, this.colorTag);
        boxOutlineTexture.Apply();
        labelStyle = new GUIStyle();
        labelStyle.fontSize = 50;
        labelStyle.normal.textColor = this.colorTag;
    }

    void Start()
    {
        if (selected_detector == Detectors.Yolo2_tiny)
        {
            detector = GameObject.Find("Detector Yolo2-tiny").GetComponent<DetectorYolo2>();
        }
        else if (selected_detector == Detectors.Yolo3_tiny)
        {
            detector = GameObject.Find("Detector Yolo3-tiny").GetComponent<DetectorYolo3>();
        }
        else
        {
            Debug.Log("DEBUG: Invalid detector model");
        }

        this.detector.Start();

        CalculateShift(this.detector.IMAGE_SIZE);
    }

    // void OnDestroy()
    // {
    //     this.detector.Stop();
    // }

    void OnEnable()
    {
        if (m_CameraManager != null)
        {
            m_CameraManager.frameReceived += OnCameraFrameReceived;
        }
    }

    void OnDisable()
    {
        if (m_CameraManager != null)
        {
            m_CameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    public void OnRefresh()
    {
        Debug.Log("DEBUG: onRefresh, removing anchors and boundingboxes");
        localization = false;
        stabilityCounter = 0;
        inferenceCounter = 0;
        rawImageCounter = 0;
        groupBoxingCounter = 0;
        // clear boubding box containers
        boxSavedOutlines.Clear();
        boxOutlines.Clear();
        // clear anchor
        AnchorCreator anchorCreator = FindObjectOfType<AnchorCreator>();
        anchorCreator.RemoveAllAnchors();
    }

    unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        // Attempt to get the latest camera image. If this method succeeds,
        // it acquires a native resource that must be disposed (see below).
        XRCpuImage image;
        if (!cameraManager.TryAcquireLatestCpuImage(out image))
        {
            return;
        }
        rawImageCounter++;
        Debug.Log($"DEBUG: cpu image acquired {rawImageCounter}");

        // Once we have a valid XRCameraImage, we can access the individual image "planes"
        // (the separate channels in the image). XRCameraImage.GetPlane provides
        // low-overhead access to this data. This could then be passed to a
        // computer vision algorithm. Here, we will convert the camera image
        // to an RGBA texture (and draw it on the screen).

        // Choose an RGBA format.
        // See XRCameraImage.FormatSupported for a complete list of supported formats.
        var format = TextureFormat.RGBA32;

        if (m_Texture == null || m_Texture.width != image.width || m_Texture.height != image.height)
        {
            m_Texture = new Texture2D(image.width, image.height, format, false);
        }

        // Convert the image to format, flipping the image across the Y axis.
        // We can also get a sub rectangle, but we'll get the full image here.
        var conversionParams = new XRCpuImage.ConversionParams(image, format, XRCpuImage.Transformation.None);

        // Texture2D allows us write directly to the raw texture data
        // This allows us to do the conversion in-place without making any copies.
        var rawTextureData = m_Texture.GetRawTextureData<byte>();
        try
        {
            image.Convert(conversionParams, new IntPtr(rawTextureData.GetUnsafePtr()), rawTextureData.Length);
        }
        finally
        {
            // We must dispose of the XRCameraImage after we're finished
            // with it to avoid leaking native resources.
            image.Dispose();
        }

        // Apply the updated texture data to our texture
        m_Texture.Apply();

        // If bounding boxes are static for certain frames, start localization
        if (stabilityCounter > 150)
        {
            localization = true;
        }
        else
        {
            // detect object and create current frame outlines
            TFDetect();
            // merging outliens across frames
            GroupBoxOutlines();
        }
        // Set the RawImage's texture so we can visualize it.
        m_RawImage.texture = m_Texture;

    }

    public void OnGUI()
    {
        // Do not draw bounding boxes after localization.
        if (localization)
        {
            return;
        }

        foreach (var box in this.boxSavedOutlines)
        {
            DrawBoxOutline(box, scaleFactor, shiftX, shiftY);
        }
    }

    // merging bounding boxes and save result to boxSavedOutlines
    private void GroupBoxOutlines()
    {
        // First call, add object recognition output to boxSavedOutlines when becomes available
        if (this.boxSavedOutlines.Count == 0)
        {
            // there's still no output from the object recognition model
            if (this.boxOutlines == null || this.boxOutlines.Count == 0)
            {
                return;
            }
            // use last output for boxes drawing and return
            foreach (var outline in this.boxOutlines)
            {
                // outline.UniqueID = outline.UniqueID + "prime";
                this.boxSavedOutlines.Add(outline);
            }
            return;
        }

        groupBoxingCounter++;
        Debug.Log($"DEBUG: box grouping {groupBoxingCounter} | boxes: {this.boxOutlines.Count}, saved: {this.boxSavedOutlines.Count}");

        // Next loop is for comparing overlapping boxes from the last result against current boxes results
        // It retains the boxes with the highest Confidence results
        bool stableFrame = true;
        List<BoundingBox> itemsToSave = new List<BoundingBox>();
        List<BoundingBox> itemsToDispose = new List<BoundingBox>();
        BoundingBox toSave, toRemove;
        foreach (var newBoxResult in this.boxOutlines)
        {
            bool unique = true;
            foreach (var savedBoxResult in this.boxSavedOutlines)
            {
                // if two bounding boxes are for the same object, use high confidnece one
                if (IsSameObject(newBoxResult, savedBoxResult))
                {
                    unique = false;
                    toSave = toRemove = null;
                    if (newBoxResult.Confidence > savedBoxResult.Confidence + 0.05F) //& savedBoxResult.Confidence < 0.5F)
                    {
                        // The new result is better than the past box, there is no stability. The counter is reset.
                        toSave = newBoxResult;
                        toRemove = savedBoxResult;
                        stableFrame = false;
                        stabilityCounter = 0;
                    } else {
                        toSave = savedBoxResult;
                        toRemove = newBoxResult;
                    }
                    itemsToSave.Add(toSave);
                    itemsToDispose.Add(toRemove);
                    Debug.Log($"DEBUG: Repeated | Add Label: {toSave.Label}. Confidence: {toSave.Confidence}.");
                }
            }

            // A new Box has been detected, reset stability counter and save it by default
            if (unique)
            {
                stableFrame = false;
                stabilityCounter = 0;
                itemsToSave.Add(newBoxResult);
                Debug.Log($"Add Label: {newBoxResult.Label}. Confidence: {newBoxResult.Confidence}.");
            }
        }

        Debug.Log($"saved before temporal merge {this.boxSavedOutlines.Count} boxes. ToSave {itemsToSave.Count}, ToDispose {itemsToDispose.Count}");
        this.boxSavedOutlines.Clear();
        this.boxSavedOutlines.AddRange(itemsToSave);
        this.boxSavedOutlines.RemoveAll(obj => itemsToDispose.Contains(obj));
        Debug.Log($"saved after temporal merge {this.boxSavedOutlines.Count} boxes");

        if (stableFrame)
        {
            stabilityCounter += 1;
        }

        // Remove stacked bounding boxes
        var tmp = new List<BoundingBox>();
        foreach (var savedBox in this.boxSavedOutlines)
        {
            tmp.Add(savedBox);
        }
        itemsToSave = new List<BoundingBox>();
        itemsToDispose = new List<BoundingBox>();
        foreach (var savedBox in this.boxSavedOutlines)
        {
            var unique = true;
            foreach (var tmpBox in tmp)
            {
                if (IsSameObject(savedBox, tmpBox))
                {
                    unique = false;
                    if (savedBox.Confidence > tmpBox.Confidence) {
                        toSave = savedBox;
                        toRemove = tmpBox;
                    } else {
                        toSave = tmpBox;
                        toRemove = savedBox;
                    }
                }
            }
            if (unique) {
                itemsToSave.Add(savedBox);
            }
        }

        Debug.Log($"saved before spatial merge {this.boxSavedOutlines.Count} boxes. ToSave {itemsToSave.Count}, ToDispose {itemsToDispose.Count}");
        this.boxSavedOutlines.Clear();
        this.boxSavedOutlines.AddRange(itemsToSave);
        this.boxSavedOutlines.RemoveAll(obj => itemsToDispose.Contains(obj));
        Debug.Log($"saved after spatial merge {this.boxSavedOutlines.Count} boxes");
    }

    // For two bounding boxes, if at least one center is inside the other box,
    // treate them as the same object.
    private bool IsSameObject(BoundingBox outline1, BoundingBox outline2)
    {
        var xMin1 = outline1.Dimensions.X * this.scaleFactor + this.shiftX;
        var width1 = outline1.Dimensions.Width * this.scaleFactor;
        var yMin1 = outline1.Dimensions.Y * this.scaleFactor + this.shiftY;
        var height1 = outline1.Dimensions.Height * this.scaleFactor;
        float center_x1 = xMin1 + width1 / 2f;
        float center_y1 = yMin1 + height1 / 2f;

        var xMin2 = outline2.Dimensions.X * this.scaleFactor + this.shiftX;
        var width2 = outline2.Dimensions.Width * this.scaleFactor;
        var yMin2 = outline2.Dimensions.Y * this.scaleFactor + this.shiftY;
        var height2 = outline2.Dimensions.Height * this.scaleFactor;
        float center_x2 = xMin2 + width2 / 2f;
        float center_y2 = yMin2 + height2 / 2f;

        bool cover_x = (xMin2 < center_x1) && (center_x1 < (xMin2 + width2));
        bool cover_y = (yMin2 < center_y1) && (center_y1 < (yMin2 + height2));
        bool contain_x = (xMin1 < center_x2) && (center_x2 < (xMin1 + width1));
        bool contain_y = (yMin1 < center_y2) && (center_y2 < (yMin1 + height1));

        return (cover_x && cover_y) || (contain_x && contain_y);
    }

    private void CalculateShift(int inputSize)
    {
        int smallest;

        if (Screen.width < Screen.height)
        {
            smallest = Screen.width;
            this.shiftY = (Screen.height - smallest) / 2f;
        }
        else
        {
            smallest = Screen.height;
            this.shiftX = (Screen.width - smallest) / 2f;
        }

        this.scaleFactor = smallest / (float)inputSize;
    }

    private void TFDetect()
    {
        if (this.isDetecting)
        {
            return;
        }

        this.isDetecting = true;
        StartCoroutine(ProcessImage(this.detector.IMAGE_SIZE, result =>
        {
            inferenceCounter++;
            var detectionID = inferenceCounter;
            var stopwatch = Stopwatch.StartNew();
            Debug.Log($"DEBUG: detection started {detectionID}");
            StartCoroutine(this.detector.Detect(result, boxes =>
            {
                stopwatch.Stop();
                Debug.Log($"DEBUG: detection finished {detectionID} in {stopwatch.ElapsedMilliseconds}ms found {boxes.Count} boxes");
                this.boxOutlines = boxes;
                Resources.UnloadUnusedAssets();
                this.isDetecting = false;
            }));
        }));
    }


    private IEnumerator ProcessImage(int inputSize, System.Action<Color32[]> callback)
    {
        Coroutine croped = StartCoroutine(TextureTools.CropSquare(m_Texture,
           TextureTools.RectOptions.Center, snap =>
           {
               var scaled = Scale(snap, inputSize);
               var rotated = Rotate(scaled.GetPixels32(), scaled.width, scaled.height);
               callback(rotated);
           }));
        yield return croped;
    }


    private void DrawBoxOutline(BoundingBox outline, float scaleFactor, float shiftX, float shiftY)
    {
        var x = outline.Dimensions.X * scaleFactor + shiftX;
        var width = outline.Dimensions.Width * scaleFactor;
        var y = outline.Dimensions.Y * scaleFactor + shiftY;
        var height = outline.Dimensions.Height * scaleFactor;

        DrawRectangle(new Rect(x, y, width, height), 10, this.colorTag);
        DrawLabel(new Rect(x, y - 80, 200, 20), $"Localizing {outline.Label}: {(int)(outline.Confidence * 100)}%");
    }


    public static void DrawRectangle(Rect area, int frameWidth, Color color)
    {
        Rect lineArea = area;
        lineArea.height = frameWidth;
        GUI.DrawTexture(lineArea, boxOutlineTexture); // Top line

        lineArea.y = area.yMax - frameWidth;
        GUI.DrawTexture(lineArea, boxOutlineTexture); // Bottom line

        lineArea = area;
        lineArea.width = frameWidth;
        GUI.DrawTexture(lineArea, boxOutlineTexture); // Left line

        lineArea.x = area.xMax - frameWidth;
        GUI.DrawTexture(lineArea, boxOutlineTexture); // Right line
    }


    private static void DrawLabel(Rect position, string text)
    {
        GUI.Label(position, text, labelStyle);
    }

    private Texture2D Scale(Texture2D texture, int imageSize)
    {
        var scaled = TextureTools.scaled(texture, imageSize, imageSize, FilterMode.Bilinear);
        return scaled;
    }


    private Color32[] Rotate(Color32[] pixels, int width, int height)
    {
        var rotate = TextureTools.RotateImageMatrix(
                pixels, width, height, 90);
        // var flipped = TextureTools.FlipYImageMatrix(rotate, width, height);
        //flipped =  TextureTools.FlipXImageMatrix(flipped, width, height);
        // return flipped;
        return rotate;
    }
}
