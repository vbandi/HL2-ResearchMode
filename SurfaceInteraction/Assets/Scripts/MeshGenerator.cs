using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using DataStructures.ViliWonka.KDTree;
using DefaultNamespace;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SocialPlatforms;
using Debug = UnityEngine.Debug;

// Surface mesh - a négyzethálós mesh-t simítsuk rá a point cloudra, méghozzá úgy, hogy az egyes vertexeknél elvégezzük legközelebbi x pont távolságának minimalizálását, a vertex x koordinátáját mozgatva. 


[RequireComponent(typeof(MeshFilter))]
public class MeshGenerator : MonoBehaviour
{
    private Mesh _mesh;
    private Vector3[] _vertices;
    private int[] _triangles;

    public int XSize = 10;
    public int ZSize = 10;

    public int NumberOfPointsForAverage = 100;

    public bool ExecuteSmoothMesh = true;
    
    private Vector3[] _pointCloud;

    private KDTree _pointCloudKDTree;
    private KDQuery _query;

    void Start()
    {
        _mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = _mesh;
        
        var sampleParticleData = SampleData.PointCloud.Split(',').Select(float.Parse).ToArray();
        
        var size = sampleParticleData.Length / 3;
        _pointCloud = new Vector3[size];

        for (int i = 0; i < size; i++)
        {
            var xIndex = i * 3;

            _pointCloud[i] = new Vector3(sampleParticleData[xIndex], sampleParticleData[xIndex + 1],
                sampleParticleData[xIndex + 2]);
        }

        _pointCloudKDTree = new KDTree(_pointCloud, 100);
        _query = new KDQuery();
        
        FindObjectOfType<PointCloudVisualizer>().SetParticles(_pointCloud, Vector3.zero);

        CreateShape();
        UpdateMesh();
        
        if (ExecuteSmoothMesh)
            StartCoroutine(nameof(SmoothMeshOnPointCloud));
    }

    private void CreateShape()
    {
        _vertices = new Vector3[(XSize + 1) * (ZSize + 1)];

        for (int i = 0, z = 0; z <= ZSize; z++)
        {
            for (int x = 0; x <= XSize; x++)
            {
                _vertices[i] = new Vector3(x, 0, z);
                i++;
            }
        }

        _triangles = new int[XSize * ZSize * 6];

        int vert = 0;
        int tris = 0;

        for (int z = 0; z < ZSize; z++)
        {
            for (int x = 0; x < XSize; x++)
            {
                _triangles[tris + 0] = vert + 0;
                _triangles[tris + 1] = vert + XSize + 1;
                _triangles[tris + 2] = vert + 1;
                _triangles[tris + 3] = vert + 1;
                _triangles[tris + 4] = vert + XSize + 1;
                _triangles[tris + 5] = vert + XSize + 2;

                vert++;
                tris += 6;
            }

            vert++;
        }
    }

    private IEnumerator SmoothMeshOnPointCloud()
    {
        var sw = new Stopwatch();
        sw.Start();
        var tr = transform; 
        for (var i = 0; i < _vertices.Length; i++)
        {
            var vertex = _vertices[i];
            var avg = GetAverageOnPointCloud(tr.TransformPoint(vertex), tr.up);
            _vertices[i] = tr.InverseTransformPoint(avg);

            if (sw.ElapsedMilliseconds > 100)
            {
                UpdateMesh();
                yield return new WaitForEndOfFrame();
                sw.Restart();
            }
        }
        
        Debug.Log($"SmoothMeshOnPointCloud took {sw.ElapsedMilliseconds} ms");
        
        UpdateMesh();
        yield return null;
    }

    private Vector3 GetAverageOnPointCloud(Vector3 point, Vector3 up)
    {
        var closestPointResult = new List<int>();
        _query.ClosestPoint(_pointCloudKDTree, point, closestPointResult);
        
        var closestPoints = GetClosestPoints(_pointCloud[closestPointResult.First()], _pointCloud, NumberOfPointsForAverage);
        var plane = new Plane(up, point);

        var sum = 0f;
        for (var i = 0; i < closestPoints.Length; i++)
        {
            var p = closestPoints[i];
            sum += plane.GetDistanceToPoint(p);
        }
        
        return new Vector3(point.x, point.y + sum / closestPoints.Length, point.z);
    }
    
    // copied here so that I can optimize it separately
    private Vector3[] GetClosestPoints(Vector3 middle, Vector3[] points, int count)
    {
        Profiler.BeginSample(nameof(GetClosestPoints));
          
        // var comparer = new DistanceComparer(middle);
        // var result = points.OrderBy(p => p, comparer).Take(count).ToArray();

        // KDTRee
        List<int> results = new List<int>();
        _query.KNearest(_pointCloudKDTree, middle, count, results);
        
        var result = results.Select(i => points[i]).ToArray();
        
        Profiler.EndSample();
        return result;
    }


    private void UpdateMesh()
    {
        _mesh.Clear();
        _mesh.vertices = _vertices;
        _mesh.triangles = _triangles;
        
        _mesh.RecalculateNormals();
    }

}
