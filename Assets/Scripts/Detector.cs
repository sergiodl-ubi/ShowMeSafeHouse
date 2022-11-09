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

public class ImgDimensions
{
    public int Width { get; set; }
    public int Height { get; set; }
    public override string ToString() => base.ToString() + " w:" + Width.ToString() + " h:" + Height.ToString();
}

public class DimensionsBase
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Height { get; set; }
    public float Width { get; set; }

    public override string ToString()
    {
        return base.ToString() + $" x:{X} y:{Y} h:{Height} w:{Width}";
    }
}

public enum BoundingBoxSize
{
    Small = 1, // <= 20%, no segmentation, just 1 segment
    Medium = 2, // 20% < box < 40%, create 4 segments
    Big = 3, // 40% < box < 70%, create 9 segments
    Large = 4 // >= 70% of the screen area, create 16 segments
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

    private Rect _rect = Rect.zero;
    public Rect Rect
    {
        get
        {
            if (_rect == Rect.zero)
            {
                _rect = new Rect(Dimensions.X, Dimensions.Y, Dimensions.Width, Dimensions.Height);
            }
            return _rect;
        }
    }

    private float _area = 0;
    public float Area
    {
        get
        {
            if (_area == 0)
            {
                _area = Dimensions.Width * Dimensions.Height;
            }
            return _area;
        }
    }

    private float screenArea = Screen.width * Screen.height;

    private BoundingBoxSize getSize()
    {
        var ratio = Area / screenArea;
        switch (ratio)
        {
            case <= 0.2F: return BoundingBoxSize.Small;
            case < 0.4F: return BoundingBoxSize.Medium;
            case < 0.7F: return BoundingBoxSize.Big;
            default: return BoundingBoxSize.Large;
        }
    }

    private Dictionary<int, BoundingBox> _segments = new Dictionary<int, BoundingBox>();
    public Dictionary<int, BoundingBox> Segments
    {
        get
        {
            if (_segments.Count > 0)
            {
                return _segments;
            }

            int divisor = (int)getSize(); // parts in which each dimension will be divided
            int divisibleWidth = nextMultipleOf(divisor, (int)Dimensions.X);
            int divisibleHeight = nextMultipleOf(divisor, (int)Dimensions.Y);
            int segmentWidth = divisibleWidth / divisor;
            int segmentHeight = divisibleHeight / divisor;
            int boxX = (int)Dimensions.X;
            int boxY = (int)Dimensions.Y;
            int boxW = (int)Dimensions.Width;
            int boxH = (int)Dimensions.Height;
            int x, y, w, h = 0;
            BoundingBox tmp;
            for (var widthN = 0; widthN < divisor; widthN++)
            {
                x = boxX + (widthN * segmentWidth);
                w = (divisor - 1) == widthN ?
                    (boxX + boxW) - x :
                    segmentWidth;
                for (var heightN = 0; heightN < divisor; heightN++)
                {
                    y = boxY + (heightN * segmentHeight);
                    h = (divisor - 1) == heightN ?
                        (boxY + boxH) - y :
                        segmentHeight;
                    tmp = new BoundingBox(
                        new BoundingBoxDimensions { X = x, Y = y, Width = w, Height = h },
                        Label, 0, false
                    );
                    _segments.Add(tmp.BoxId, tmp);
                }
            }
            return _segments;
        }
    }

    public BoundingBox(BoundingBoxDimensions dims, string label, float confidence, bool used)
    {
        _dims = dims;
        _label = label;
        _confidence = confidence;
        Used = used;
        setBoxId();
    }

    /// <summary>
    /// Returns the next multiple of <c>number</c> that surpass or is equal to <c>lowerLimit</c>
    /// </summary>
    private int nextMultipleOf(int number, int lowerLimit) => (((lowerLimit - 1) / number) + 1) * number;

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
        return $"{Label}:{Confidence}, ({Dimensions.X},{Dimensions.Y}) - {Dimensions.Width}x{Dimensions.Height}";
    }
}
