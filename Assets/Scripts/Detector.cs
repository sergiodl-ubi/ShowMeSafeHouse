using System;
using UnityEngine;
using Unity.Barracuda;
using System.Collections;
using System.Collections.Generic;

public interface Detector
{
    int IMAGE_SIZE { get; }
    void Start();
    IEnumerator Detect(Color32[] picture, System.Action<IList<BoundingBox>> callback);

}

public class DimensionsBase
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Height { get; set; }
    public float Width { get; set; }
}


public class BoundingBoxDimensions : DimensionsBase { }

class CellDimensions : DimensionsBase { }


public class BoundingBox : IEquatable<BoundingBox>
{
    private BoundingBoxDimensions _dims;
    public BoundingBoxDimensions Dimensions { get => _dims; set { _dims = value; setBoxId(); } }

    private string _label;
    public string Label { get => _label; set { _label = value; setBoxId(); } }

    private float _confidence;
    public float Confidence { get => _confidence; set { _confidence = value; setBoxId(); } }

    // whether the bounding box already is used to raycast anchors
    public bool Used { get; set; }

    private int _boxId = 0;
    public int BoxId { get => _boxId; }

    public BoundingBox(BoundingBoxDimensions dims, string label, float confidence, bool used)
    {
        _dims = dims;
        _label = label;
        _confidence = confidence;
        Used = used;
        setBoxId();
    }

    public Rect Rect
    {
        get => new Rect(Dimensions.X, Dimensions.Y, Dimensions.Width, Dimensions.Height);
    }

    private void setBoxId() => GetHashCode();

    public override int GetHashCode()
    {
        if (_boxId == 0)
        {
            _boxId = (
                Dimensions.X, Dimensions.Y, Dimensions.Width, Dimensions.Height,
                Label, Confidence
            ).GetHashCode();
        }
        return _boxId;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        BoundingBox otherBox = obj as BoundingBox;
        if (otherBox == null) return false;
        else return Equals(otherBox);
    }

    public bool Equals(BoundingBox box)
    {
        if (box == null) return false;
        return this.GetHashCode() == box.GetHashCode();
    }

    public override string ToString()
    {
        return $"{Label}:{Confidence}, {Dimensions.X}:{Dimensions.Y} - {Dimensions.Width}:{Dimensions.Height}";
    }
}
