using System.Collections.Generic;
using UnityEngine;

public static class ObjectClasses
{
    public static Dictionary<string, List<string>> Guides = new Dictionary<string, List<string>>()
    {
        {
            "はこ", new List<string> {
                "ドアをふさいでいない",
                "通路をふさいでいない",
            }
        },
        {
            "モニター", new List<string> {
                "ベッドから離れている",
                "台か壁に固定している",
            }
        },
        {
            "れいぞうこ", new List<string> {
                "ストッパーを搭載している",
                "倒れても出口をふさがない",
            }
        },
        {
            "レンジ", new List<string> {
                "滑り止めマットの上に置いている",
                "開いても頭をぶつけない",
            }
        },
        {
            "テレビ", new List<string> {
                "ベッドから離れている",
                "台か壁に固定している",
            }
        },
        {
            "ドア", new List<string> {
                "ふさぐものがない",
                "何かが転がっても、ふさがらない",
            }
        },
        {
            "ベッド", new List<string> {
                "上に重いものを置いていない",
            }
        },
        {
            "かしつき", new List<string> {
                "ストッパーを搭載している",
            }
        },
        {
            "プリンター", new List<string> {
                "滑り止めマットの上に置いている",
            }
        },
        {
            "引き出し", new List<string> {
                "セーフティロックを搭載している",
            }
        },
        {
            "パソコン", new List<string> {
                "滑り止めマットの上に置いている",
                "床に落ちない",
            }
        },
    };

    public static Dictionary<string, Vector3> BoundingCubeScales = new Dictionary<string, Vector3>()
    {
        { "はこ", new Vector3(0.2f, 0.2f, 0.2f) },
        { "モニター", new Vector3(0.3f, 0.3f, 0.1f) },
        { "れいぞうこ", new Vector3(0.3f, 0.6f, 0.3f) },
        { "レンジ", new Vector3(0.3f, 0.2f, 0.3f) },
        { "テレビ", new Vector3(0.4f, 0.6f, 0.4f) },
        { "ドア", new Vector3(0.4f, 1.2f, 0.4f) },
        { "ベッド", new Vector3(0.8f, 0.4f, 0.8f) },
        { "かしつき", new Vector3(0.3f, 0.6f, 3f) },
        { "プリンター", new Vector3(0.4f, 0.4f, 0.4f) },
        { "ドロワー", new Vector3(0.4f, 0.6f, 0.4f) },
        { "パソコン", new Vector3(0.3f, 0.4f, 0.3f) },
    };
}