# ShowMe! A Safe House
**A mobile augmented reality research application that helps people identify household earthquake hazards and understand how to address them.**

### Citation

```
@article{ARAppforPreparednessGuidelines,
  title = {Mobile Augmented Reality Interface for Instruction-based Disaster Preparedness Guidelines},
  author = {Sergio De León Aguilar and Yuki Matsuda and Keiichi Yasumoto},
  journal = {Sensors and Materials},
  month = {10},
  number = {10},
  pages = {4585--4585},
  publisher = {MYU K.K.},
  doi = {10.18494/sam5215},
  url = {https://doi.org/10.18494/SAM5215},
  volume = {36},
  year = {2024}
}
```

## Purpose

ShowMe! A Safe House explores how disaster preparedness advice can be made easier to interpret and apply in a person's own surroundings. Printed guidelines describe general examples; users must recognize which objects and situations in their homes those examples apply to. The application connects that advice to objects seen through a smartphone camera, supporting the transfer of preparedness knowledge from experts to the general public.

The application identifies objects with a YOLO object detection model and uses Unity's AR Foundation to place interactive, object-specific checklists near them in the 3D environment. Short prompts help users assess conditions such as whether furniture is secured, heavy objects could fall onto a bed, or an exit could become blocked. Users inspect their surroundings and mark checklist items as satisfied; a cartoon status indicator changes with checklist completion.

![demo](DocResources/Interface.gif)

## Use in the research study

This application was the experimental AR interface in [*Mobile AR Interface for Instruction-based Disaster Preparedness Guidelines*](#citation). The study examined whether AR guidance could reduce the burden of interpreting expert advice, improve engagement, and help users identify earthquake preparedness actions compared with a traditional printed guideline. This application was tested on an iPhone 12 using a YOLOv5s model adapted through transfer learning on a custom dataset of experimental objects.

## Design considerations

- **Familiar, accessible hardware:** Consumer smartphones were selected for their availability and familiarity across ages, particularly to reduce entry barriers and technology rejection among older users. Unity supported development for multiple mobile platforms, while the study used iPhone 12 and ARKit. Processing limits and the need to hold the phone during use were practical constraints.
- **Advice placed in context:** Checklists were anchored near recognized objects to support "near-transfer learning": connecting an instruction directly to the real situation where it applies. Object labels and the camera view supply context, allowing brief prompts instead of long explanations.
- **Simple interaction and readable text:** Familiar checklists present advice as short, individual assertions. The study design used bold white text over translucent black backgrounds, with Helvetica for English and Noto Sans JP for Japanese. These choices emphasized familiarity and readability; they did not address every accessibility need, such as dyslexia.
- **Visual feedback:** A cartoon character moves through fearful, worried, and relaxed expressions as checkboxes are completed. This was intended to encourage engagement, especially among younger users. Its state reflects checklist completion, not an independently verified assessment of room safety.
- **Stable recognition on mobile hardware:** Lightweight object detection was chosen for its balance of accuracy and computational cost. A time-wise non-maximum suppression algorithm combines consecutive detections and retains stronger candidates, reducing transient false positives and flickering detections before placing checklists.
- **Placement without dedicated depth sensors:** Detected bounding boxes are subdivided according to their screen area. The placement algorithm favors segments with more AR feature points to improve anchor placement where depth estimates are unreliable, including around large, smooth surfaces such as screens.

## Supported detection models

  * YOLOv5s from [Ultralytics YOLOv5](https://github.com/ultralytics/yolov5), without integrated NMS.
  * YOLOv3-tiny.
  * YOLOv2-tiny.

## Requirements
  * Unity 2021.3 (project version: 2021.3.27f1)
  * Packages
```
"com.unity.barracuda": "3.0.0",
"com.unity.xr.arfoundation": "4.2.8",
"com.unity.textmeshpro": "3.0.6",
"com.unity.ugui": "1.0.0",
```

## Acknowledgement
* Base project taken from [Unity_Detection2AR](https://github.com/derenlei/Unity_Detection2AR).
  * There, partial code borrowed from:
  * [TFClassify-Unity-Barracuda](https://github.com/Syn-McJ/TFClassify-Unity-Barracuda).
  * [arfoundation-samples](https://github.com/Unity-Technologies/arfoundation-samples).
* [AR Simulation package](https://github.com/needle-tools/ar-simulation) is only free for noncommercial use.

## Enhancements

* Support for YOLOv5s alongside YOLOv2-tiny and YOLOv3-tiny.
* Time-wise detection filtering for stable multi-object recognition and anchoring.
* Bounding-box segmentation and feature-point-based checklist placement.
* Object-specific interactive preparedness checklists and a visual completion indicator.
* Updates for Unity 2021.3 and the package versions listed above.
