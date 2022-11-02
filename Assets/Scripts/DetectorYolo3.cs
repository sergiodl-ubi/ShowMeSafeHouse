using System;
using UnityEngine;
using Unity.Barracuda;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Stopwatch = System.Diagnostics.Stopwatch;

public class DetectorYolo3 : MonoBehaviour, Detector
{

    public NNModel modelFile;
    public TextAsset labelsFile;

    private const int IMAGE_MEAN = 0;
    private const float IMAGE_STD = 255.0F;

    // ONNX model input and output name. Modify when switching models.
    //These aren't const values because they need to be easily edited on the component before play mode

    public string INPUT_NAME;
    public string OUTPUT_NAME_L;
    public string OUTPUT_NAME_M;
    public YOLOVer DETECTOR_VERSION;

    //This has to stay a const
    private const int _image_size = 416;
    public int IMAGE_SIZE { get => _image_size; }

    // Minimum detection confidence to track a detection
    public float MINIMUM_CONFIDENCE = 0.50f;

    private IWorker worker;

    // public const int ROW_COUNT_L = 13;
    // public const int COL_COUNT_L = 13;
    // public const int ROW_COUNT_M = 26;
    // public const int COL_COUNT_M = 26;
    public Dictionary<string, int> params_l = new Dictionary<string, int>() { { "ROW_COUNT", 13 }, { "COL_COUNT", 13 }, { "CELL_WIDTH", 32 }, { "CELL_HEIGHT", 32 } }; // yv3
    public Dictionary<string, int> params_m = new Dictionary<string, int>() { { "ROW_COUNT", 26 }, { "COL_COUNT", 26 }, { "CELL_WIDTH", 16 }, { "CELL_HEIGHT", 16 } }; // yv3
    public const int BOXES_PER_CELL = 3;
    public const int BOX_INFO_FEATURE_COUNT = 5;
    public enum YOLOVer
    {
        v3t,
        v4t,
        v5s,
    }


    //Update this!
    public int CLASS_COUNT;

    // public const float CELL_WIDTH_L = 32;
    // public const float CELL_HEIGHT_L = 32;
    // public const float CELL_WIDTH_M = 16;
    // public const float CELL_HEIGHT_M = 16;
    private string[] labels;

    private float[] anchors = new float[]
    {
        10F, 14F,  23F, 27F,  37F, 58F,  81F, 82F,  135F, 169F,  344F, 319F // yolov3-tiny
    };
    private float[] anchors_v4t = new float[]
    {
        69F, 48F,  46F, 279F,  133F, 128F,  332F, 129F,  180F, 315F,  371F, 363F // yolov4-tiny
    };
    private Stopwatch process_timer;
    private PhoneARCamera phoneARCamera;


    void Awake()
    {
        phoneARCamera = GameObject.Find("Camera Image").GetComponent<PhoneARCamera>();
    }

    public void Start()
    {
        this.labels = Regex.Split(this.labelsFile.text, "\n|\r|\r\n")
            .Where(s => !String.IsNullOrEmpty(s)).ToArray();
        var model = ModelLoader.Load(this.modelFile);
        // https://docs.unity3d.com/Packages/com.unity.barracuda@1.0/manual/Worker.html
        //These checks all check for GPU before CPU as GPU is preferred if the platform + rendering pipeline support it
        this.worker = GraphicsWorker.GetWorker(model);
    }

    // TODO: Stop(), unload worker and model

    public IEnumerator Detect(Color32[] picture, System.Action<IList<BoundingBox>> callback)
    {
        using (var tensor = TransformInput(picture, IMAGE_SIZE, IMAGE_SIZE))
        {
            var inputs = new Dictionary<string, Tensor>();
            inputs.Add(INPUT_NAME, tensor);
            process_timer = Stopwatch.StartNew();
            /*
            var executor = worker.StartManualSchedule(inputs);
            //worker.Execute(inputs);
            while (executor.MoveNext()) {
                // Debug.Log($"Worker progress: {worker.scheduleProgress}");
                worker.FlushSchedule();
                yield return null;
            }
            */
            var output = worker.Execute(inputs).PeekOutput(OUTPUT_NAME_L);
            yield return new WaitForCompletion(output);
            var recognitionTime = process_timer.ElapsedMilliseconds;

            IList<BoundingBox> results = new List<BoundingBox>();
            if (DETECTOR_VERSION == YOLOVer.v3t)
            {
                var output_m = worker.PeekOutput(OUTPUT_NAME_M);
                Debug.Log("Output " + OUTPUT_NAME_L + ": " + output);
                Debug.Log("Output " + OUTPUT_NAME_M + ": " + output_m);
                var results_l = ParseOutputs(output, MINIMUM_CONFIDENCE, params_l);
                var results_m = ParseOutputs(output_m, MINIMUM_CONFIDENCE, params_m);
                results = results_l.Concat(results_m).ToList();
            }
            else if (DETECTOR_VERSION == YOLOVer.v4t)
            {
                var output_l = worker.PeekOutput(OUTPUT_NAME_L);
                var output_m = worker.PeekOutput(OUTPUT_NAME_M);
                Debug.Log("Output " + OUTPUT_NAME_L + ": " + output_l);
                Debug.Log("Output " + OUTPUT_NAME_M + ": " + output_m);
                results = ParseYV4Output(output_l, output_m, MINIMUM_CONFIDENCE);
            }
            else if (DETECTOR_VERSION == YOLOVer.v5s)
            {
                // var output = worker.PeekOutput(OUTPUT_NAME_L);
                //Debug.Log("Output " + OUTPUT_NAME_L + ": " + output);
                results = ParseYV5sOutput(output, MINIMUM_CONFIDENCE);
            }
            var parseTime = process_timer.ElapsedMilliseconds - recognitionTime;
            var boxes = FilterBoundingBoxes(results, 5, MINIMUM_CONFIDENCE);
            var nmsTime = process_timer.ElapsedMilliseconds - recognitionTime - parseTime;
            var totalTime = nmsTime + parseTime + recognitionTime;
            process_timer.Stop();
            Debug.Log($"Finish output postprocess: recogTime({recognitionTime}ms) parseTime({parseTime}ms) nmsTime({nmsTime}ms) total({totalTime})");
            Debug.Log($"{boxes.Count} boxes found:");
            for (var i = 0; i < boxes.Count; i++)
            {
                Debug.Log(boxes[i].ToString());
            }
            callback(boxes);
        }
    }


    public static Tensor TransformInput(Color32[] pic, int width, int height)
    {
        float[] floatValues = new float[width * height * 3];

        for (int i = 0; i < pic.Length; ++i)
        {
            var color = pic[i];

            floatValues[i * 3 + 0] = (color.r - IMAGE_MEAN) / IMAGE_STD;
            floatValues[i * 3 + 1] = (color.g - IMAGE_MEAN) / IMAGE_STD;
            floatValues[i * 3 + 2] = (color.b - IMAGE_MEAN) / IMAGE_STD;
        }

        return new Tensor(1, height, width, 3, floatValues);
    }

    private IList<BoundingBox> ParseYV4Output(Tensor boxesOutput, Tensor classesOutput, float threshold)
    {
        var boxes = new List<BoundingBox>();
        var boxesCount = boxesOutput.shape.channels;
        for (int boxIdx = 0; boxIdx < 5; boxIdx++)
        {
            var X = boxesOutput[0, 0, 0, boxIdx];
            var Y = boxesOutput[0, 0, 1, boxIdx];
            var Width = boxesOutput[0, 0, 2, boxIdx];
            var Height = boxesOutput[0, 0, 3, boxIdx];
            Debug.Log($"x:{X}, y:{Y}, width:{Width}, height:{Height}. Shape {boxesOutput.shape}");

            var sw = Screen.width;
            var sh = Screen.height;

            var confidencesString = "";
            float[] confidencesSig = new float[CLASS_COUNT];
            for (int confIdx = 0; confIdx < CLASS_COUNT; confIdx++)
            {
                // var sigmoided = Sigmoid(classesOutput[0, 0, confIdx, boxIdx]);
                confidencesString += classesOutput[0, 0, confIdx, boxIdx].ToString() + ", ";
                // confidencesSig[confIdx] = sigmoided;
            }
            Debug.Log("Confidences: " + confidencesString + $" Shape {classesOutput.shape}");
            var predictedClasses = Softmax(confidencesSig);
            // Debug.Log("PredictedClasses: " + predictedClasses.ToString());
        }
        return boxes;
    }
    private IList<BoundingBox> ParseYV5sOutput(Tensor boxes, float threshold)
    {
        var boundingBoxes = new List<BoundingBox>();
        var boxesCount = boxes.shape.channels;

        for (int boxIdx = 0; boxIdx < boxesCount; boxIdx++)
        {
            var ObjConf = boxes[0, 0, 4, boxIdx];
            if (ObjConf < 0.35)
            {
                continue;
            }
            // Tensor data [x, y, w, h, obj_conf, [class_conf],] for each channel
            var X = boxes[0, 0, 0, boxIdx];
            var Y = boxes[0, 0, 1, boxIdx];
            var Width = boxes[0, 0, 2, boxIdx];
            var Height = boxes[0, 0, 3, boxIdx];

            float[] predictedClasses = new float[CLASS_COUNT];
            for (int predictedClass = 0; predictedClass < CLASS_COUNT; predictedClass++)
            {
                predictedClasses[predictedClass] = boxes[0, 0, 5 + predictedClass, boxIdx] * ObjConf; // Cond Prob ObjClass | ObjInBox
            }
            var (ClassIdx, ClassConf) = GetTopResult(predictedClasses);

            var Conf = ClassConf * ObjConf; // Conditional Probability of Object Class given Object in bounding box
            var ClassName = labels[ClassIdx];

            Debug.Log($"Normalized vals x:{X}, y:{Y}, width:{Width}, height:{Height}, conf:{Conf}, class:{ClassName}: clsConf{ClassConf}|objConf{ObjConf}");
            var origDims = phoneARCamera.imgDimensions;
            var croppedDims = phoneARCamera.croppedImgDimensions;
            float xScale = croppedDims.Width / IMAGE_SIZE;
            float yScale = croppedDims.Height / IMAGE_SIZE; /*
            X = (X * xScale) + ((origDims.Width - croppedDims.Width) / 2); // Redimension of bouding boxes is done in AnchorCreator script
            Y = (Y * yScale) + ((origDims.Height - croppedDims.Height) / 2);
            Width *= xScale;
            Height *= yScale;
            Debug.Log($"Processed vals x:{X}, y:{Y}, width:{Width}, height:{Height}");
            */
            boundingBoxes.Add(new BoundingBox(
                new BoundingBoxDimensions
                {
                    X = (X - Width / 2), // Converting (center_x, center_y) to (x1, y1), top left corner of bounding box
                    Y = (Y - Height / 2),
                    Width = Width,
                    Height = Height
                },
                ClassName, Conf, false
            ));
        }

        return boundingBoxes;
    }

    private IList<BoundingBox> ParseOutputs(Tensor yoloModelOutput, float threshold, Dictionary<string, int> parameters)
    {
        var boxes = new List<BoundingBox>();

        for (int cy = 0; cy < parameters["COL_COUNT"]; cy++)
        {
            for (int cx = 0; cx < parameters["ROW_COUNT"]; cx++)
            {
                for (int box = 0; box < BOXES_PER_CELL; box++)
                {
                    var channel = (box * (CLASS_COUNT + BOX_INFO_FEATURE_COUNT));
                    var bbd = ExtractBoundingBoxDimensions(yoloModelOutput, cx, cy, channel);
                    float confidence = GetConfidence(yoloModelOutput, cx, cy, channel);

                    if (confidence < threshold)
                    {
                        continue;
                    }

                    float[] predictedClasses = ExtractClasses(yoloModelOutput, cx, cy, channel);
                    var (topResultIndex, topResultScore) = GetTopResult(predictedClasses);
                    var topScore = topResultScore * confidence;
                    Debug.Log("DEBUG: results: " + topResultIndex.ToString());

                    if (topScore < threshold)
                    {
                        continue;
                    }

                    var mappedBoundingBox = MapBoundingBoxToCell(cx, cy, box, bbd, parameters);
                    var newBox = new BoundingBox(
                        new BoundingBoxDimensions
                        {
                            X = (mappedBoundingBox.X - mappedBoundingBox.Width / 2),
                            Y = (mappedBoundingBox.Y - mappedBoundingBox.Height / 2),
                            Width = mappedBoundingBox.Width,
                            Height = mappedBoundingBox.Height,
                        },
                        labels[topResultIndex],
                        topScore,
                        false
                    );
                    boxes.Add(newBox);
                }
            }
        }

        return boxes;
    }


    private float Sigmoid(float value)
    {
        var k = (float)Math.Exp(value);

        return k / (1.0f + k);
    }


    private float[] Softmax(float[] values)
    {
        var maxVal = values.Max();
        var exp = values.Select(v => Math.Exp(v - maxVal));
        var sumExp = exp.Sum();

        return exp.Select(v => (float)(v / sumExp)).ToArray();
    }


    private BoundingBoxDimensions ExtractBoundingBoxDimensions(Tensor modelOutput, int x, int y, int channel)
    {
        return new BoundingBoxDimensions
        {
            X = modelOutput[0, x, y, channel],
            Y = modelOutput[0, x, y, channel + 1],
            Width = modelOutput[0, x, y, channel + 2],
            Height = modelOutput[0, x, y, channel + 3]
        };
    }


    private float GetConfidence(Tensor modelOutput, int x, int y, int channel)
    {
        //Debug.Log("ModelOutput " + modelOutput);
        return Sigmoid(modelOutput[0, x, y, channel + 4]);
    }


    private CellDimensions MapBoundingBoxToCell(int x, int y, int box, BoundingBoxDimensions boxDimensions, Dictionary<string, int> parameters)
    {
        return new CellDimensions
        {
            X = ((float)y + Sigmoid(boxDimensions.X)) * parameters["CELL_WIDTH"],
            Y = ((float)x + Sigmoid(boxDimensions.Y)) * parameters["CELL_HEIGHT"],
            Width = (float)Math.Exp(boxDimensions.Width) * anchors[6 + box * 2],
            Height = (float)Math.Exp(boxDimensions.Height) * anchors[6 + box * 2 + 1],
        };
    }


    public float[] ExtractClasses(Tensor modelOutput, int x, int y, int channel)
    {
        float[] predictedClasses = new float[CLASS_COUNT];
        int predictedClassOffset = channel + BOX_INFO_FEATURE_COUNT;

        for (int predictedClass = 0; predictedClass < CLASS_COUNT; predictedClass++)
        {
            predictedClasses[predictedClass] = modelOutput[0, x, y, predictedClass + predictedClassOffset];
        }

        return Softmax(predictedClasses);
    }


    private ValueTuple<int, float> GetTopResult(float[] predictedClasses)
    {
        return predictedClasses
            .Select((predictedClass, index) => (Index: index, Value: predictedClass))
            .OrderByDescending(result => result.Value)
            .First();
    }


    private float IntersectionOverUnion(Rect boundingBoxA, Rect boundingBoxB)
    {
        var areaA = boundingBoxA.width * boundingBoxA.height;

        if (areaA <= 0)
            return 0;

        var areaB = boundingBoxB.width * boundingBoxB.height;

        if (areaB <= 0)
            return 0;

        var minX = Math.Max(boundingBoxA.xMin, boundingBoxB.xMin);
        var minY = Math.Max(boundingBoxA.yMin, boundingBoxB.yMin);
        var maxX = Math.Min(boundingBoxA.xMax, boundingBoxB.xMax);
        var maxY = Math.Min(boundingBoxA.yMax, boundingBoxB.yMax);

        var intersectionArea = Math.Max(maxY - minY, 0) * Math.Max(maxX - minX, 0);

        return intersectionArea / (areaA + areaB - intersectionArea);
    }


    private IList<BoundingBox> FilterBoundingBoxes(IList<BoundingBox> boxes, int limit, float threshold)
    {
        var activeCount = boxes.Count;
        var isActiveBoxes = new bool[boxes.Count];

        for (int i = 0; i < isActiveBoxes.Length; i++)
        {
            isActiveBoxes[i] = true;
        }

        var sortedBoxes = boxes.Select((b, i) => new { Box = b, Index = i })
                .OrderByDescending(b => b.Box.Confidence)
                .ToList();

        var results = new List<BoundingBox>();

        for (int i = 0; i < boxes.Count; i++)
        {
            if (isActiveBoxes[i])
            {
                var boxA = sortedBoxes[i].Box;
                results.Add(boxA);

                if (results.Count >= limit)
                    break;

                for (var j = i + 1; j < boxes.Count; j++)
                {
                    if (isActiveBoxes[j])
                    {
                        var boxB = sortedBoxes[j].Box;

                        if (IntersectionOverUnion(boxA.Rect, boxB.Rect) > threshold)
                        {
                            isActiveBoxes[j] = false;
                            activeCount--;

                            if (activeCount <= 0)
                                break;
                        }
                    }
                }

                if (activeCount <= 0)
                    break;
            }
        }
        return results;
    }
}
