using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

public class SurfaceDrawerBase : MonoBehaviour
{
    public Ruler Ruler;
    protected readonly List<Vector3> _points = new List<Vector3>();
    protected LineRenderer _line;
    protected int spatialAwarenessLayerId;

    private DrawingMode _drawingMode = DrawingMode.Freehand;

    private Plane _lastPlane;
    
    public DrawingMode DrawingMode
    {
        get => _drawingMode;
        set
        {
            _drawingMode = value;
            Draw(_lastPlane);
        }
    }
    
    protected void AddPoint(Vector3 point, Plane plane)
    {
        _lastPlane = plane;
        _points.Add(point);
        Draw(plane);
    }

    private void Draw(Plane plane)
    {
        if (_points.Count > 1)
        {
            Ruler.SetPoints(_points.First(), _points.Last(), plane);

            switch (DrawingMode)
            {
                case DrawingMode.None:
                    _line.positionCount = 0;
                    _line.SetPositions(new Vector3[]{});
                    break;
                case DrawingMode.Freehand:
                    DrawFreehand();
                    break;
                case DrawingMode.Circle:
                    DrawCircle(plane);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void DrawCircle(Plane plane)
    {
        var startPointOnPlane = plane.ClosestPointOnPlane(_points.First());
        var endPointOnPlane = plane.ClosestPointOnPlane(_points.Last());

        var radius = (endPointOnPlane - startPointOnPlane).magnitude;

        var go = new GameObject();
        go.transform.SetPositionAndRotation(startPointOnPlane, Quaternion.LookRotation(
            plane.ClosestPointOnPlane(startPointOnPlane + Camera.main.transform.forward.normalized) - startPointOnPlane, 
            plane.normal));
        
        var segments = 360;
        // _line.useWorldSpace = false;
        _line.positionCount = segments + 1;

        var pointCount = segments + 1; // add extra point to make startpoint and endpoint the same to close the circle
        var points = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            var rad = Mathf.Deg2Rad * (i * 360f / segments);
            points[i] = go.transform.TransformPoint(new Vector3(Mathf.Sin(rad) * radius, 0, Mathf.Cos(rad) * radius));
        }

        _line.SetPositions(points);
        
        Destroy(go);
    }

    private void DrawFreehand()
    {
        _line.positionCount = _points.Count;
        _line.SetPositions(_points.ToArray());
        _line.Simplify(0.002f);
    }

    private void Awake()
    {
        spatialAwarenessLayerId = LayerMask.GetMask("Spatial Awareness");
    }
}

public enum DrawingMode
{
    None,
    Freehand,
    Circle
}