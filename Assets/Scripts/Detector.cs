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
    public BoundingBoxDimensions Dimensions { get; set; }

    public string Label { get; set; }

    public float Confidence { get; set; }

    // whether the bounding box already is used to raycast anchors
    public bool Used { get; set; }

    public Rect Rect
    {
        get { return new Rect(Dimensions.X, Dimensions.Y, Dimensions.Width, Dimensions.Height); }
    }

    public override int GetHashCode()
    {
        return (new object[]
        {
            Dimensions.X, Dimensions.Y, Dimensions.Width, Dimensions.Height,
            Label, Confidence
        }).GetHashCode();
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
