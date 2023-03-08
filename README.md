# ShowMe! A Safe House
<strong>An application to highlight and show advice to address objects that might become hazards during an Earthquake.</strong>

This application identifies objects using a YOLO based object detection model and place a checklist interface with several pieces of advice over the recognized objects inside of the 3D World coordinate system of Unity's ARFoundation Augmented Reality environment.

Currently supports:
  * YOLOv5s from [Ultralytics YOLOv5](https://github.com/ultralytics/yolov5), without integrated NMS.
  * YOLOv3-tiny.
  * YOLOv2-tiny.

![demo](DocResources/Interface.gif)

## Requirements
  * Unity 2021.3
  * Packages
    "com.unity.barracuda": "3.0.0",
    "com.unity.xr.arfoundation": "4.2.7",
    "com.unity.textmeshpro": "3.0.6",
    "com.unity.ugui": "1.0.0",

## Configuring a new model
1. On the Scene object explorer, select the `Detector Yolo3-tiny` game object. On the right side, it will appear all public variables that can be modified.
2. Select the proper model file and labels (classes) file on the "Model file" and "Labels file" respectively.
3. Set the correct name for the INPUT layer on "INPUT_NAME".
4. For YOLOv3/2-tiny models you have to set both "OUTPUT_NAME_L" and "OUTPUT_NAME_R" output layers name. For YOLOv5, you only need to write the output name on "OUTPUT_NAME_L".
5. Select the model used in the field "DETECTOR_VERSION".
6. "CLASS_COUNT" must coincide with the number of lines (classes) found in the "Labels file".
* If you change the model to YOLOv2-tiny, you must change the detector script in the game object "Camera Image".

## Acknowledgement
* Base project taken from [Unity_Detection2AR](https://github.com/derenlei/Unity_Detection2AR).
  * There, partial code borrowed from:
  * [TFClassify-Unity-Barracuda](https://github.com/Syn-McJ/TFClassify-Unity-Barracuda).
  * [arfoundation-samples](https://github.com/Unity-Technologies/arfoundation-samples).
* [AR Simulation package](https://github.com/needle-tools/ar-simulation) is only free for noncommercial use.

## Enhancements

* Many and refactoring of some algorithms for efficiency.
* Version upgrade compatibiliy
TODO: be specific on the work done