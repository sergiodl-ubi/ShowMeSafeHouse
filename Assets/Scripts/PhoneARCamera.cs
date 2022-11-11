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
    public ImgDimensions imgDimensions = new ImgDimensions();
    public ImgDimensions croppedImgDimensions = new ImgDimensions();

    public Color colorTag = new Color(0.3843137f, 0, 0.9333333f);
    private static GUIStyle labelStyle;
    private static Texture2D boxOutlineTexture;
    // bounding boxes detected for current frame
    private Dictionary<int, BoundingBox> boxOutlines = new Dictionary<int, BoundingBox>();
    // bounding boxes detected across frames
    public Dictionary<int, BoundingBox> boxSavedOutlines = new Dictionary<int, BoundingBox>();
    // lock model when its inferencing a frame
    private bool isDetecting = false;

    // the number of frames that bounding boxes stay static
    private int stabilityCounter = 0;
    private int stableFramesNeeded = 3; // old 120
    public bool recognitionFinished = false;
    private int inferenceCounter = 0;
    private int rawImageCounter = 0;
    private int groupBoxingCounter = 0;
    private Stopwatch stabilityStopwatch;

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
        recognitionFinished = false;
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
        if (isDetecting || recognitionFinished)
        {
            return;
        }
        // Attempt to get the latest camera image. If this method succeeds,
        // it acquires a native resource that must be disposed (see below).
        XRCpuImage image;
        if (!cameraManager.TryAcquireLatestCpuImage(out image))
        {
            return;
        }
        rawImageCounter++;
        // Debug.Log($"DEBUG: cpu image acquired {rawImageCounter}");

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
        if (stabilityCounter > stableFramesNeeded)
        {
            recognitionFinished = true;
            stabilityStopwatch.Stop();
            Debug.Log($"DEBUG: recognition stabilized in {stabilityStopwatch.ElapsedMilliseconds}ms");
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
        if (recognitionFinished)
        {
            return;
        }

        foreach (BoundingBox boxToDraw in this.boxSavedOutlines.Values)
        {
            DrawBoxOutline(boxToDraw, scaleFactor, shiftX, shiftY);
        }
    }

    // merging bounding boxes and save result to boxSavedOutlines
    private void GroupBoxOutlines()
    {
        // First call, add object recognition output to boxSavedOutlines when becomes available
        if (this.boxSavedOutlines.Count < 1)
        {
            // there's still no output from the object recognition model
            if (this.boxOutlines == null || this.boxOutlines.Count < 1)
            {
                return;
            }
            // use last output for boxes drawing and return
            Debug.Log("Initializing boxSavedOutlines");
            foreach (var (boxId, box) in this.boxOutlines)
            {
                Debug.Log($"Copying box {boxId}");
                this.boxSavedOutlines.Add(boxId, box);
            }
            return;
        }

        groupBoxingCounter++;
        Debug.Log($"DEBUG: box grouping {groupBoxingCounter} | boxes: {this.boxOutlines.Count}, saved: {this.boxSavedOutlines.Count}");

        // Next loop is for comparing overlapping boxes from the last result against current boxes results
        // It retains the boxes with the highest Confidence results
        bool stableFrame = true;
        var itemsToSave = new Dictionary<int, BoundingBox>();
        var itemsToDispose = new Dictionary<int, BoundingBox>();
        BoundingBox toSave, toRemove;
        foreach (BoundingBox newBoxResult in this.boxOutlines.Values)
        {
            bool unique = true;
            foreach (BoundingBox savedBoxResult in this.boxSavedOutlines.Values)
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
                        Debug.Log($"DEBUG: Box updated | Label: {toSave.Label}@{toSave.Confidence:0.00}.");
                    }
                    else
                    {
                        toSave = savedBoxResult;
                        toRemove = newBoxResult;
                    }
                    itemsToSave.TryAdd(toSave.BoxId, toSave);
                    if (newBoxResult.BoxId != savedBoxResult.BoxId)
                    {
                        itemsToDispose.TryAdd(toRemove.BoxId, toRemove);
                        Debug.Log($"Substitute {toRemove.Label}@{toRemove.Confidence:0.00} with {toSave.Label}@{toSave.Confidence:0.00}");
                    }
                }
            }

            // A new Box has been detected, reset stability counter and save it by default
            if (unique)
            {
                stableFrame = false;
                stabilityCounter = 0;
                itemsToSave.TryAdd(newBoxResult.BoxId, newBoxResult);
                Debug.Log($"Add Label: {newBoxResult.Label}. Confidence: {newBoxResult.Confidence}.");
            }
        }

        Debug.Log($"saved before temporal merge {this.boxSavedOutlines.Count} boxes. ToSave {itemsToSave.Count}, ToDispose {itemsToDispose.Count}");
        foreach (var boxId in itemsToDispose.Keys)
        {
            itemsToSave.Remove(boxId);
        }
        this.boxSavedOutlines = new Dictionary<int, BoundingBox>(itemsToSave);
        Debug.Log($"saved after temporal merge {this.boxSavedOutlines.Count} boxes");

        if (stableFrame)
        {
            if (stabilityCounter == 0)
            {
                stabilityStopwatch = Stopwatch.StartNew();
            }
            stabilityCounter += 1;
            // if (stabilityCounter % 10 == 0)
            // {
            Debug.Log($"DEBUG: Stability Counter {stabilityCounter}");
            // }
        }
    }

    // For two bounding boxes, if at least one center is inside the other box,
    // treate them as the same object.
    private bool IsSameObject(BoundingBox outline1, BoundingBox outline2)
    {
        var xMin1 = outline1.Dimensions.X * this.scaleFactor + this.shiftX;
        var width1 = outline1.Dimensions.Width * this.scaleFactor;
        var yMin1 = outline1.Dimensions.Y * this.scaleFactor + this.shiftY;
        var height1 = outline1.Dimensions.Height * this.scaleFactor;
        float center_x1 = xMin1 + (width1 / 2f);
        float center_y1 = yMin1 + (height1 / 2f);

        var xMin2 = outline2.Dimensions.X * this.scaleFactor + this.shiftX;
        var width2 = outline2.Dimensions.Width * this.scaleFactor;
        var yMin2 = outline2.Dimensions.Y * this.scaleFactor + this.shiftY;
        var height2 = outline2.Dimensions.Height * this.scaleFactor;
        float center_x2 = xMin2 + (width2 / 2f);
        float center_y2 = yMin2 + (height2 / 2f);

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
        // imgDimensions.Width = m_Texture.height; // The img comes in landscape orientation from the camera
        // imgDimensions.Height = m_Texture.width; // So the dimensions are interchanged
        StartCoroutine(ProcessImage(this.detector.IMAGE_SIZE, processedImage =>
        {
            inferenceCounter++;
            var detectionID = inferenceCounter;
            var stopwatch = Stopwatch.StartNew();
            Debug.Log($"DEBUG: detection started {detectionID} for texture of ");
            StartCoroutine(this.detector.Detect(processedImage, boxes =>
            {
                stopwatch.Stop();
                Debug.Log($"DEBUG: detection finished {detectionID} in {stopwatch.ElapsedMilliseconds}ms found {boxes.Count} boxes");
                this.boxOutlines = new Dictionary<int, BoundingBox>();
                foreach (BoundingBox box in boxes)
                {
                    this.boxOutlines.TryAdd(box.BoxId, box);
                }
                Resources.UnloadUnusedAssets();
                this.isDetecting = false;
            }));
        }));
    }

    private IEnumerator ScaleImage(Texture2D texture, int netSize, System.Action<Texture2D> callback)
    {
        var scaled = Scale(texture, netSize);
        yield return null;
        callback(scaled);
    }

    private IEnumerator ProcessImage(int inputSize, System.Action<Color32[]> callback)
    {
        Debug.Log($"Texture original size: {m_Texture.width}x{m_Texture.height}");
        Debug.Log($"Viewport/Screen size: {Screen.width}x{Screen.height}");
        var timer = Stopwatch.StartNew();
        Coroutine croped = StartCoroutine(TextureTools.CropSquare(m_Texture,
            TextureTools.RectOptions.Center, snap =>
            {
                var ellapsedTime = timer.ElapsedMilliseconds;
                Debug.Log($"Cropping took: {ellapsedTime}ms");
                croppedImgDimensions.Width = snap.width;
                croppedImgDimensions.Height = snap.height;
                var scaled = Scale(snap, inputSize);
                ellapsedTime = timer.ElapsedMilliseconds - ellapsedTime;
                Debug.Log($"Input image array size: {scaled.width}x{scaled.height}. Scaling down took {ellapsedTime}ms");
                //var rotated = Rotate(scaled.GetPixels32(), scaled.width, scaled.height);
                //var rotated = TextureTools.Rotate90SquareMatrix(scaled.GetPixels32(), scaled.width);
                timer.Stop();
                //ellapsedTime = timer.ElapsedMilliseconds - ellapsedTime;
                //Debug.Log($"Rotating took {ellapsedTime}ms");
                //callback(rotated);

                // Landscape mode increase checkboxes long phrase readability,
                // it has been set as the default.
                // Rotation not needed anymore
                callback(scaled.GetPixels32());
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
