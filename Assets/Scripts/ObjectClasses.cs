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
                "Is fixed to wall or stand",
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
                "Is placed over anti-slip mat",
                "Open door won't hit your head",
            }
        },
        {
            "Television", new List<string> {
                "Is far from bed",
                "Is fixed to wall or stand",
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
                "Is placed over anti-slip mat",
            }
        },
        {
            "Drawer", new List<string> {
                "Has safety locks",
            }
        },
        {
            "PC", new List<string> {
                "Is placed over anti-slip mat",
                "It won't fall to the floor",
            }
        },
    };

    public static Dictionary<string, Vector3> BoundingCubeScales = new Dictionary<string, Vector3>()
    {
        { "Box", new Vector3(0.2f, 0.2f, 0.2f) },
        { "Monitor", new Vector3(0.3f, 0.3f, 0.1f) },
        { "Refrigerator", new Vector3(0.3f, 0.6f, 0.3f) },
        { "Microwave Oven", new Vector3(0.3f, 0.2f, 0.3f) },
        { "Television", new Vector3(0.4f, 0.6f, 0.4f) },
        { "Door", new Vector3(0.4f, 1.2f, 0.4f) },
        { "Bed", new Vector3(0.8f, 0.4f, 0.8f) },
        { "Humidifier", new Vector3(0.3f, 0.6f, 3f) },
        { "Printer", new Vector3(0.4f, 0.4f, 0.4f) },
        { "Drawer", new Vector3(0.4f, 0.6f, 0.4f) },
        { "PC", new Vector3(0.3f, 0.4f, 0.3f) },
    };
}