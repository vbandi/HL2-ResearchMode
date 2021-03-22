// Based on example code from https://github.com/petergu684/HoloLens2-ResearchMode-Unity/issues/1


/*
For reference, this is the interface of the native research mode APIs:
 
public sealed class HL2ResearchMode : object, IHL2ResearchMode
{
    public extern HL2ResearchMode();
    public extern ushort IHL2ResearchMode.GetCenterDepth();
    
    // Get depth map texture buffer. (For visualization purpose)
    public extern ushort[] IHL2ResearchMode.GetDepthMapBuffer();
    
    // Get depth map texture buffer. (For visualization purpose)
    public extern byte[] IHL2ResearchMode.GetDepthMapTextureBuffer();
    
    // Get the buffer for point cloud in the form of float array.
    // There will be 3n elements in the array where the 3i, 3i+1, 3i+2 element correspond to x, y, z component of the i'th point. (i->[0,n-1])
    public extern float[] IHL2ResearchMode.GetPointCloudBuffer();
    
    // Get the 3D point (float[3]) of center point in depth map. Can be used to render depth cursor.
    public extern float[] IHL2ResearchMode.GetCenterPoint();
    public extern int IHL2ResearchMode.GetBufferSize();
    public extern string IHL2ResearchMode.PrintResolution();
    public extern string IHL2ResearchMode.PrintDepthExtrinsics();
    public extern bool IHL2ResearchMode.DepthMapTextureUpdated();
    public extern bool IHL2ResearchMode.PointCloudUpdated();
    public extern void IHL2ResearchMode.InitializeDepthSensor();
    public extern void IHL2ResearchMode.StartDepthSensorLoop();
    
    // Stop the sensor loop and release buffer space.
    // Sensor object should be released at the end of the loop function    
    public extern void IHL2ResearchMode.StopAllSensorDevice();
    
    // Set the reference coordinate system. Need to be set before the sensor loop starts; otherwise, default coordinate will be used.    
    public extern void IHL2ResearchMode.SetReferenceCoordinateSystem(
        SpatialCoordinateSystem refCoord);
}
*/
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using DefaultNamespace;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine.Events;
using UnityEngine.UI;

#if ENABLE_WINMD_SUPPORT
using System.IO;
using HL2UnityPlugin;
#endif

public class SensorTest : MonoBehaviour
{
#if ENABLE_WINMD_SUPPORT
    HL2ResearchMode researchMode;
#endif

    [SerializeField]
    GameObject previewPlane = null;
    [SerializeField]
    Text text;

    [SerializeField]
    PointCloudVisualizer PointCloudVisualizer;

    public Interactable DepthSensorToggle;
    public Interactable ContinuousPointCloudToggle;
    public Interactable DumpToggle;
    public Interactable ShowPointCloud;
    public Interactable AircraftToggle;

    public GameObject AircraftMesh;
    
    private Material mediaMaterial = null;
    private Texture2D mediaTexture = null;
    private byte[] frameData = null;

    void Start()
    {
#if ENABLE_WINMD_SUPPORT
        researchMode = new HL2ResearchMode();
        researchMode.InitializeDepthSensor();
#endif
        mediaMaterial = previewPlane.GetComponent<MeshRenderer>().material;
        previewPlane.SetActive(false);
        
        ShowPointCloud.OnClick.AddListener(HandleShowPointCloudClicked);
        ShowPointCloud.IsToggled = true;
        HandleShowPointCloudClicked();
        
        DepthSensorToggle.OnClick.AddListener(HandleDepthSensorClicked);

        if (AircraftToggle != null)
            AircraftToggle.OnClick.AddListener(() => AircraftMesh.SetActive(AircraftToggle.IsToggled));
    }

    private void HandleDepthSensorClicked()
    {
        if (DepthSensorToggle.IsToggled)
            StartDepthSensingLoopEvent();
        else
            StopSensorLoopEvent();
    }

    private void HandleShowPointCloudClicked()
    {
        var b = ShowPointCloud.IsToggled;
        PointCloudVisualizer.ShowPointCloud = b;

        if (b)
            PointCloudVisualizer.ShowPoints();
        else
            PointCloudVisualizer.HidePoints();
    }

    #region Button Events
    public void PrintDepthEvent()
    {
#if ENABLE_WINMD_SUPPORT
        text.text = $"Center Depth: {researchMode.GetCenterDepth().ToString()}";
#endif
    }

    public void PrintDepthExtrinsicsEvent()
    {
#if ENABLE_WINMD_SUPPORT
        text.text = researchMode.PrintDepthExtrinsics();
#endif
    }

    public void StartDepthSensingLoopEvent()
    {
        
#if ENABLE_WINMD_SUPPORT
        IntPtr WorldOriginPtr = UnityEngine.XR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr();
        var unityWorldOrigin = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(WorldOriginPtr) as Windows.Perception.Spatial.SpatialCoordinateSystem;
        researchMode.SetReferenceCoordinateSystem(unityWorldOrigin);
        researchMode.StartDepthSensorLoop();
#endif
    }

    public void StopSensorLoopEvent()
    {
#if ENABLE_WINMD_SUPPORT
        researchMode.StopAllSensorDevice();
#endif
    }

    public void ShowParticles()
    {
        ContinuousPointCloudToggle.IsToggled = false;
        ShowParticlesInternal(DumpToggle != null && DumpToggle.IsToggled);
    }

    private void ShowParticlesInternal(bool dump)
    {
#if ENABLE_WINMD_SUPPORT
        var pointCloud = researchMode.GetPointCloudBuffer();

        if (dump)
        {
            Debug.Log($"Dumping point cloud of {pointCloud.Length / 3} points..." +
                      $"-------");

            Debug.Log(string.Join(", ", pointCloud));

            Debug.Log($"-----" +
                      $"Dump finished point cloud of {pointCloud.Length / 3} points...");
        }

        if (PointCloudVisualizer != null)
        {
            var center = researchMode.GetCenterPoint();
            var c = new Vector3(center[0], center[1], center[2]); 
            PointCloudVisualizer.SetParticles(pointCloud, c);
            // PointCloudVisualizer.CalculateQuad(c);
        }

#endif
    }

    bool startRealtimePreview = false;
    
    public void StartPreviewEvent()
    {
        startRealtimePreview = !startRealtimePreview;
        previewPlane.SetActive(startRealtimePreview);
    }
    #endregion

    
    private void LateUpdate()
    {
#if ENABLE_WINMD_SUPPORT
        PrintDepthEvent();

        if (ContinuousPointCloudToggle.IsToggled && researchMode.PointCloudUpdated())
        {
            ShowParticlesInternal(false);
        }

        // update depth map texture
        if (startRealtimePreview && researchMode.DepthMapTextureUpdated())
        {
            if (!mediaTexture)
            {
                mediaTexture = new Texture2D(512, 512, TextureFormat.Alpha8, false);
                mediaMaterial.mainTexture = mediaTexture;
            }

            byte[] frameTexture = researchMode.GetDepthMapTextureBuffer();
            if (frameTexture.Length > 0)
            {
                if (frameData == null)
                {
                    frameData = frameTexture;
                }
                else
                {
                    System.Buffer.BlockCopy(frameTexture, 0, frameData, 0, frameData.Length);
                }

                if (frameData != null)
                {
                    mediaTexture.LoadRawTextureData(frameData);
                    mediaTexture.Apply();
                }
            }
        }
#endif
    }
}