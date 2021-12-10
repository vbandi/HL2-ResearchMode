using System;
using System.Collections.Generic;
using System.Linq;
using DefaultNamespace;
using Microsoft.MixedReality.Toolkit.Utilities;
using NaughtyAttributes;
using UniRx;
using UniRx.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using Logger = UniRx.Diagnostics.Logger;
using Random = UnityEngine.Random;
#if ENABLE_WINMD_SUPPORT
using HL2UnityPlugin;
#endif

public class ObservableResearchModeData : IDisposable
{
#if ENABLE_WINMD_SUPPORT
    private HL2ResearchMode _researchMode;
#endif
    
    private static ObservableResearchModeData _instance;
    public static ObservableResearchModeData Instance => _instance ?? (_instance = new ObservableResearchModeData());
    
    private ObservableResearchModeData()
    {}

    private CompositeDisposable _subscriptions;
    
    /// <summary>
    /// Indicated whether to use long or short throw mode
    /// </summary>
    public readonly BoolReactiveProperty LongThrowMode = new BoolReactiveProperty(false);
    
    /// <summary>
    /// Whether research mode is running
    /// </summary>
    public readonly BoolReactiveProperty IsRunning = new BoolReactiveProperty(false);
    
    /// <summary>
    /// Observable stream of point detected raw point cloud and center coordinates 
    /// </summary>
    public readonly Subject<(Vector3[] pointCloud, Vector3 center)> PointCloud = new Subject<(Vector3[], Vector3)>();
    
    /// <summary>
    /// Observable stream of depth map texture
    /// </summary>
    public readonly Subject<byte[]> DepthMapTexture = new Subject<byte[]>();

    /// <summary>
    /// Observable stream of <see cref="NumberOfClosesPointsForNormalCalculation"/> closest points to the center
    /// </summary>
    public readonly Subject<IEnumerable<Vector3>> ClosestPoints = new Subject<IEnumerable<Vector3>>();

    /// <summary>
    /// Observable stream of noise reduced surface normals around the center point
    /// </summary>
    public readonly Subject<(Vector3 center, Vector3 normal)> SurfaceNormal = new Subject<(Vector3 center, Vector3 normal)>();
    
    /// <summary>
    /// Observable stream of surface quads created by <see cref="SurfaceQuadFactory"/>,
    /// moved to an average "height" around the center point 
    /// </summary>
    public readonly Subject<Transform> SurfaceQuad = new Subject<Transform>();

    /// <summary>
    /// The quad factory function
    /// </summary>
    public Func<Transform> SurfaceQuadFactory;
    
    /// <summary>
    /// The minimum triangle size for the normal noise reduction
    /// </summary>
    public float MinTriangleSize = 0.03f;
    
    /// <summary>
    /// The number of closest points for normal calculation
    /// </summary>
    public int NumberOfClosesPointsForNormalCalculation = 10000;
    
    /// <summary>
    /// The number of triangles for normal calculation
    /// </summary>
    public int NumberOfTrianglesForNormal = 1000;

    /// <summary>
    /// If true, surface quad calculation is paused
    /// </summary>
    public bool PauseSurfaceQuadCalculation = false;

    /// <summary>
    /// Observable stream of the distance of the center point from the user's head
    /// </summary>
    public Subject<float> CenterDistance { get; } = new Subject<float>();

    /// <summary>
    /// Observable stream of Center coordinates
    /// </summary>
    public Subject<Vector3> Center { get; } = new Subject<Vector3>();

    public void Start()
    {
#if ENABLE_WINMD_SUPPORT
        if (_researchMode != null) // already running
            return;

        _subscriptions?.Dispose();
        _subscriptions = new CompositeDisposable();

        Debug.Log($"Starting research mode at {DateTime.Now}");

        try
        {
            _researchMode = new HL2ResearchMode();
            
            if (LongThrowMode.Value)
                _researchMode.InitializeLongDepthSensor(); 
            else
                _researchMode.InitializeDepthSensor();
        }
        catch (Exception ex)
        {
            Debug.LogError($"ERRRROOOORRR: '{ex.Message}'");
            throw;
        }

        Debug.Log($"Started research mode at {DateTime.Now}");
        Debug.Log($"Depth Extrinsics: {_researchMode.PrintDepthExtrinsics()}");
        
        IntPtr WorldOriginPtr = UnityEngine.XR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr();
        var unityWorldOrigin = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(WorldOriginPtr) as Windows.Perception.Spatial.SpatialCoordinateSystem;
        _researchMode.SetReferenceCoordinateSystem(unityWorldOrigin);

        if (LongThrowMode.Value)  // TODO: merge with the other if? 
            _researchMode.StartLongDepthSensorLoop();
        else
            _researchMode.StartDepthSensorLoop();

        Observable.EveryUpdate().Where(_ => IsRunning.Value).Subscribe(_ =>
        {
            CenterDistance.OnNext(_researchMode.GetCenterDepth());
            var c = _researchMode.GetCenterPoint();
            Center.OnNext(new Vector3(c[0], c[1], c[2]));
        }).AddTo(_subscriptions);

        if (LongThrowMode.Value)
        {
            Observable.EveryUpdate()
                .Where(_ => IsRunning.Value && DepthMapTexture.HasObservers && _researchMode.LongDepthMapTextureUpdated())
                .Subscribe(_ => HandleDepthMapTextureUpdated()).AddTo(_subscriptions);

            Observable.EveryUpdate().Where(_ => IsRunning.Value && PointCloud.HasObservers &&_researchMode.LongThrowPointCloudUpdated())
                .Subscribe(_ => HandlePointCloudUpdated(_researchMode.GetCenterPoint(), _researchMode.GetLongThrowPointCloudBuffer()))
                .AddTo(_subscriptions);
        }
        else
        {
            Observable.EveryUpdate()
                .Where(_ => IsRunning.Value && DepthMapTexture.HasObservers && _researchMode.DepthMapTextureUpdated())
                .Subscribe(_ => HandleDepthMapTextureUpdated()).AddTo(_subscriptions);

            Observable.EveryUpdate().Where(_ => IsRunning.Value && PointCloud.HasObservers && _researchMode.PointCloudUpdated())
                .Subscribe(_ => HandlePointCloudUpdated(_researchMode.GetCenterPoint(), _researchMode.GetPointCloudBuffer()))
                .AddTo(_subscriptions);
        }
        
        IsRunning.Value = true;
        
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
    }
    
    private void HandleDepthMapTextureUpdated()
    {
        
#if ENABLE_WINMD_SUPPORT
        DepthMapTexture.OnNext(LongThrowMode.Value ? _researchMode.GetLongDepthMapTextureBuffer() : _researchMode.GetDepthMapTextureBuffer());
#endif

    }

    private void HandlePointCloudUpdated(float[] c, float[] points)
    {
        Profiler.BeginSample(nameof(HandlePointCloudUpdated));
        
        // var c = _researchMode.GetCenterPoint();
        Vector3 center = new Vector3(c[0], c[1], c[2]);

        // var pointCloud = _researchMode.GetPointCloudBuffer();
        
        var size = points.Length / 3;
        var pointCloud = new Vector3[size];

        for (int i = 0; i < size; i++)
        {
            var xIndex = i * 3;
            pointCloud[i] = new Vector3(points[xIndex], points[xIndex + 1], points[xIndex + 2]);
        }
        
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

        if (!SurfaceQuad.HasObservers || PauseSurfaceQuadCalculation)
            return;

        var quad = SurfaceQuadFactory.Invoke();
        MoveToAverageCenter(quad, closestPoints, center, normal);
        SurfaceQuad.OnNext(quad);
        
        Profiler.EndSample();
    }

    private Vector3 CalculateNormal(Vector3 center, Vector3[] points, float minTriangleSize)
    {
        Profiler.BeginSample(nameof(CalculateNormal));
        var result = new Vector3();

        var axis = Camera.main.transform.position - center;
            
        // get numberOfTrianglesForNormal number of triangles with a minimum angle and side size
        for (int i = 0; i < NumberOfTrianglesForNormal; i++) 
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
        Profiler.EndSample();
        
        return result.normalized;
    }

    private Vector3 NormalOfTriangle(Vector3 pointA, Vector3 pointB, Vector3 pointC)
    {
        Profiler.BeginSample(nameof(NormalOfTriangle));
        var v1 = pointC - pointA;
        var v2 = pointB - pointA;
        var result = Vector3.Cross(v1, v2).normalized;
        Profiler.EndSample();
        return result;
    }
    
    private void MoveToAverageCenter(Transform target, Vector3[] closestPoints, Vector3 center, Vector3 normal)
    {
        Profiler.BeginSample(nameof(MoveToAverageCenter));
        target.position = center;
        target.LookAt(center - normal);

        var sumx = 0f;
        var sumy = 0f;
        var sumz = 0f;

        for (var i = 0; i < closestPoints.Length; i++)
        {
            var closestPoint = closestPoints[i];
            var point = target.InverseTransformPoint(closestPoint);
            sumx += point.x;
            sumy += point.y;
            sumz += point.z;
        }

        target.Translate(0, 0, sumz / closestPoints.Length * target.localScale.z, Space.Self);
        Profiler.EndSample();
    }

    
    public static Vector3[] GetClosestPoints(Vector3 middle, Vector3[] points, int count)
    {
        Profiler.BeginSample(nameof(GetClosestPoints));
        var size = points.Length / 3;

        var comparer = new DistanceComparer(middle);
        var result = points.OrderBy(p => p, comparer).Take(count).ToArray();
        
        Profiler.EndSample();
        return result;
    }

    public void Stop()
    {
        Debug.Log("Stopping research mode");
        IsRunning.Value = false;
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
        _instance = null;
    }
}

public class DistanceComparer : IComparer<Vector3>
{
    private Vector3 _origin;
    public DistanceComparer(Vector3 origin)
    {
        _origin = origin;
    }

    public int Compare(Vector3 x, Vector3 y)
    {
        return (x - _origin).sqrMagnitude.CompareTo((y - _origin).sqrMagnitude);
    }
}