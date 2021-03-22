using System;
using System.Collections.Generic;
using System.Linq;
using DefaultNamespace;
using Microsoft.MixedReality.Toolkit.Utilities;
using NaughtyAttributes;
using UniRx;
using UniRx.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;
using Logger = UniRx.Diagnostics.Logger;
using Random = UnityEngine.Random;
#if ENABLE_WINMD_SUPPORT
using System.Threading.Tasks;
using HL2UnityPlugin;

#endif

public class ObservableResearchModeData : IDisposable
{
#if ENABLE_WINMD_SUPPORT
    private HL2ResearchMode _researchMode;
#endif

    private CompositeDisposable _subscriptions;
    
    public readonly Subject<(float[] pointCloud, Vector3 center)> PointCloud = new Subject<(float[], Vector3)>();
    public readonly Subject<byte[]> DepthMapTexture = new Subject<byte[]>();

    public readonly Subject<IEnumerable<Vector3>> ClosestPoints = new Subject<IEnumerable<Vector3>>();
    public int NumberOfClosesPointsForNormalCalculation = 1000;

    public readonly Subject<(Vector3 center, Vector3 normal)> SurfaceNormal = new Subject<(Vector3 center, Vector3 normal)>();
    public readonly Subject<Transform> SurfaceQuad = new Subject<Transform>();

    public Func<Transform> SurfaceQuadFactory;
    
    public float MinTriangleSize;

    // public BoolReactiveProperty EnablePointCloud { get; } = new BoolReactiveProperty(false);
    //
    // public BoolReactiveProperty EnableDepthMapTexture { get; } = new BoolReactiveProperty(false);
    //
    public Subject<float> CenterDistance { get; } = new Subject<float>();

    public Subject<Vector3> Center { get; } = new Subject<Vector3>();

    public void Start()
    {
#if ENABLE_WINMD_SUPPORT
        if (_researchMode != null) // already running
            return;

        _subscriptions?.Dispose();
        _subscriptions = new CompositeDisposable();

        Debug.Log($"Starting research mode at {DateTime.Now}");
        
        _researchMode = new HL2ResearchMode();
        _researchMode.InitializeDepthSensor();

        Debug.Log($"Started research mode at {DateTime.Now}");
        Debug.Log($"Depth Extrinsics: {_researchMode.PrintDepthExtrinsics()}");
        
        IntPtr WorldOriginPtr = UnityEngine.XR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr();
        var unityWorldOrigin = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(WorldOriginPtr) as Windows.Perception.Spatial.SpatialCoordinateSystem;
        _researchMode.SetReferenceCoordinateSystem(unityWorldOrigin);
        _researchMode.StartDepthSensorLoop();

        Observable.EveryUpdate().Subscribe(_ =>
        {
            CenterDistance.OnNext(_researchMode.GetCenterDepth());
            var c = _researchMode.GetCenterPoint();
            Center.OnNext(new Vector3(c[0], c[1], c[2]));
        }).AddTo(_subscriptions);

        Observable.EveryUpdate().Where(_ => DepthMapTexture.HasObservers && _researchMode.DepthMapTextureUpdated())
            .Subscribe(_ => HandleDepthMapTextureUpdated()).AddTo(_subscriptions);
        
        Observable.EveryUpdate().Where(_ => PointCloud.HasObservers && _researchMode.PointCloudUpdated())
            .Subscribe(_ => HandlePointCloudUpdated(_researchMode.GetCenterPoint(), _researchMode.GetPointCloudBuffer())).AddTo(_subscriptions);
#endif
    }

    public void LoadSampleData(Vector3 mainCameraPosition)
    {
        var sampleParticleData = SampleData.PointCloud.Split(',').Select(float.Parse).ToArray();
        var middleIndex = sampleParticleData.Length / 2 + 600;  //TODO: this is not how the middle is counted because there's a lot of variation between the number of points
        middleIndex -= middleIndex % 3;  // make sure it falls to an actual point

        HandlePointCloudUpdated(
            new[]
            {
                sampleParticleData[middleIndex], sampleParticleData[middleIndex + 1],
                sampleParticleData[middleIndex + 2]
            }, sampleParticleData
        );
        
        // var center = new Vector3(sampleParticleData[middleIndex], sampleParticleData[middleIndex + 1],
        //     sampleParticleData[middleIndex + 2]);
        //
        // PointCloud.OnNext((sampleParticleData, center));
        // CenterDistance.OnNext(Vector3.Distance(mainCameraPosition, center));
        // Center.OnNext(center);
    }
    
    private void HandleDepthMapTextureUpdated()
    {
        
#if ENABLE_WINMD_SUPPORT
        DepthMapTexture.OnNext(_researchMode.GetDepthMapTextureBuffer());
#endif

    }

    private void HandlePointCloudUpdated(float[] c, float[] pointCloud)
    {
        // var c = _researchMode.GetCenterPoint();
        Vector3 center = new Vector3(c[0], c[1], c[2]);

        // var pointCloud = _researchMode.GetPointCloudBuffer();
        PointCloud.OnNext((pointCloud, center));

        if (!(ClosestPoints.HasObservers || SurfaceNormal.HasObservers || SurfaceQuad.HasObservers))
            return;

        var closestPoints =
            GetClosestPoints(center, pointCloud, NumberOfClosesPointsForNormalCalculation);
            
        ClosestPoints.OnNext(closestPoints);

        if (!(SurfaceNormal.HasObservers || SurfaceQuad.HasObservers))
            return;

        var normal = CalculateNormal(center, closestPoints, MinTriangleSize);
        SurfaceNormal.OnNext((center, normal));

        if (!SurfaceQuad.HasObservers)
            return;

        var quad = SurfaceQuadFactory.Invoke();
        MoveToAverageCenter(quad, closestPoints, center, normal);
        SurfaceQuad.OnNext(quad);
    }
    
    public Vector3 CalculateNormal(Vector3 center, Vector3[] points, float minTriangleSize)
    {
        var result = new Vector3();

        var axis = Camera.main.transform.position - center;
            
        // get numberOfTrianglesForNormal number of triangles with a minimum angle and side size
        for (int i = 0; i < NumberOfClosesPointsForNormalCalculation; i++) 
        {
            var a = points[Random.Range(0, points.Length)];
            var b = points[Random.Range(0, points.Length)];
            var c = points[Random.Range(0, points.Length)];

            var angle = Vector3.SignedAngle(a - c, a - b, axis);

            // skip if triangle is not large enough
            if (angle < 30 || angle > 120 || (a - b).magnitude < minTriangleSize || (a - c).magnitude < minTriangleSize ||
                (b - c).magnitude < minTriangleSize)
            {
                i = i - 1;
                continue;
            }

            result += NormalOfTriangle(a, b, c);
        }
        return result.normalized;
    }
    
    public Vector3 NormalOfTriangle(Vector3 pointA, Vector3 pointB, Vector3 pointC)
    {
        var v1 = pointC - pointA;
        var v2 = pointB - pointA;
        return Vector3.Cross(v1, v2).normalized;
    }
    
    private void MoveToAverageCenter(Transform target, Vector3[] closestPoints, Vector3 center, Vector3 normal)
    {
        // if (closestPoints.Count < numberOfClosesPointsForNormalCalculation / 3)
        //     return;
            
        // if (Normal != null)
        // {
        target.position = center;
        target.LookAt(center - normal);
            //
            // Quad.position = center;
            // Quad.LookAt(center - Normal.Value);
        // }

        var sumx = 0f;
        var sumy = 0f;
        var sumz = 0f;

        foreach (var closestPoint in closestPoints)
        {
            var point = target.InverseTransformPoint(closestPoint);
            sumx += point.x;
            sumy += point.y;
            sumz += point.z;
            // Debug.Log(point.z);
        }

        // Debug.Log($"avgx: {sumx / _closestPoints.Count}, avgy: {sumy / _closestPoints.Count}, avgz: {sumz / _closestPoints.Count}");

        target.Translate(0, 0, sumz / closestPoints.Length * target.localScale.z, Space.Self);
    }

    
    private Vector3[] GetClosestPoints(Vector3 middle, float[] points, int count)
    {
        var size = points.Length / 3;

        var comparer = new DistanceComparer(middle);
        var result = new SortedSet<Vector3>(comparer);

        for (int i = 0; i < size; i++)
        {
            int xIndex = i * 3;
            result.Add(new Vector3(points[xIndex], points[xIndex + 1], points[xIndex + 2]));
        }

        return result.Take(count).ToArray();
    }

    public void Stop()
    {
        Debug.Log("Stopping research mode");
        _subscriptions?.Dispose();
        
#if ENABLE_WINMD_SUPPORT
        _researchMode?.StopAllSensorDevice();
        _researchMode = null;
#endif
        
    }

    // ReSharper disable Unity.PerformanceAnalysis
    public void Dispose()
    {
        Stop();
    }
}