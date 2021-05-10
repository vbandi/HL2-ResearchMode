using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SurfaceDrawerBase : MonoBehaviour
{
    public Ruler Ruler;
    protected readonly List<Vector3> points = new List<Vector3>();
    protected LineRenderer _line;

    protected void AddPoint(Vector3 point)
    {
        points.Add(point);
        _line.positionCount = points.Count;
        _line.SetPositions(points.ToArray());

        if (points.Any())
        {
            Ruler.From = points.First();
            Ruler.To = points.Last();
        }
    }
}