using System.Collections.Generic;
using UnityEngine;

public static class ObjectClasses
{
    public static Dictionary<string, List<string>> Guides = new Dictionary<string, List<string>>()
    {
        {
            "Box", new List<string> {
                "Is not blocking doors",
                "Is not on corridors",
            }
        },
        {
            "Monitor", new List<string> {
                "Is far from bed",
                "Has fasteners",
            }
        },
        {
            "Refrigerator", new List<string> {
                "Has breaks installed",
                "Will not block exit if falls",
            }
        },
        {
            "Microwave Oven", new List<string> {
                "Is placed over sticky pads",
                "Open door won't hit your head",
            }
        },
        {
            "Television", new List<string> {
                "Is far from bed",
                "Has fasteners",
            }
        },
        {
            "Door", new List<string> {
                "There's nothing blocking it",
                "Nothing will roll and block it",
            }
        },
        {
            "Bed", new List<string> {
                "No heavy stuff above",
            }
        },
        {
            "Humidifier", new List<string> {
                "Has breaks installed",
            }
        },
        {
            "Printer", new List<string> {
                "Will not fall over feet",
            }
        },
        {
            "Drawer", new List<string> {
                "Has safety locks",
            }
        },
        {
            "PC", new List<string> {
                "Is placed over sticky pads",
                "Won't fall to the floor",
            }
        },
    };

    public static Dictionary<string, Vector3> BoundingCubeScales = new Dictionary<string, Vector3>()
    {
        { "Box", new Vector3(1f, 1f, 1f) },
        { "Monitor", new Vector3(1f, 1f, 1f) },
        { "Refrigerator", new Vector3(1f, 1f, 1f) },
        { "Microwave Oven", new Vector3(1f, 1f, 1f) },
        { "Television", new Vector3(1f, 1f, 1f) },
        { "Door", new Vector3(1f, 1f, 1f) },
        { "Bed", new Vector3(1f, 1f, 1f) },
        { "Humidifier", new Vector3(1f, 1f, 1f) },
        { "Printer", new Vector3(1f, 1f, 1f) },
        { "Drawer", new Vector3(1f, 1f, 1f) },
        { "PC", new Vector3(1f, 1f, 1f) },
    };
}