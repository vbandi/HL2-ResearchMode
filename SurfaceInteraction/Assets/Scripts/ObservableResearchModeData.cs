using System;
using System.Linq;
using DefaultNamespace;
using NaughtyAttributes;
using UniRx;
using UniRx.Diagnostics;
using UnityEngine;
using Logger = UniRx.Diagnostics.Logger;
#if ENABLE_WINMD_SUPPORT

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
            .Subscribe(_ => HandlePointCloudUpdated()).AddTo(_subscriptions);
#endif
    }

    public void LoadSampleData(Vector3 mainCameraPosition)
    {
        var sampleParticleData = SampleData.PointCloud.Split(',').Select(float.Parse).ToArray();
        var middleIndex = sampleParticleData.Length / 2 + 600;  //TODO: this is not how the middle is counted because there's a lot of variation between the number of points
        middleIndex -= middleIndex % 3;  // make sure it falls to an actual point

        var center = new Vector3(sampleParticleData[middleIndex], sampleParticleData[middleIndex + 1],
            sampleParticleData[middleIndex + 2]);

        PointCloud.OnNext((sampleParticleData, center));
        CenterDistance.OnNext(Vector3.Distance(mainCameraPosition, center));
        Center.OnNext(center);
    }
    
    private void HandleDepthMapTextureUpdated()
    {
        
#if ENABLE_WINMD_SUPPORT
        DepthMapTexture.OnNext(_researchMode.GetDepthMapTextureBuffer());
#endif

    }

    private void HandlePointCloudUpdated()
    {
        
#if ENABLE_WINMD_SUPPORT

        var c = _researchMode.GetCenterPoint();
        Vector3 center = new Vector3(c[0], c[1], c[2]);

        PointCloud.OnNext((_researchMode.GetPointCloudBuffer(), center));

#endif

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