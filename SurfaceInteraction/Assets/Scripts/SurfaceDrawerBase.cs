using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SurfaceDrawerBase : MonoBehaviour
{
    public Ruler Ruler;
    protected readonly List<Vector3> points = new List<Vector3>();
    protected LineRenderer _line;
    protected int spatialAwarenessLayerId;

    protected void AddPoint(Vector3 point, Plane plane)
    {
        points.Add(point);
        _line.positionCount = points.Count;
        _line.SetPositions(points.ToArray());

        if (points.Any())
        {
            Ruler.SetPoints(points.First(), points.Last(), plane);
        }
    }

    private void Awake()
    {
        spatialAwarenessLayerId = LayerMask.GetMask("Spatial Awareness");
    }
}