using System;
using DefaultNamespace;
using Microsoft.MixedReality.Toolkit.UI;
using NaughtyAttributes;
using UniRx;
using UniRx.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    public Interactable DepthSensorToggle;
    public Interactable PreviewToggle;
    public Interactable ContinuousToggle;
    public Interactable ShowPointCloudToggle;
    public Interactable PointCloudSnapshotButton;
    public Text Text;
    public GameObject Preview;

    public PointCloudVisualizer PointCloudVisualizer;

    private Material _mediaMaterial = null;
    private Texture2D _mediaTexture = null;

    private ObservableResearchModeData _researchModeData;
    private IDisposable _previewSubscription;
    private IDisposable _pointCloudSubscription;
    
#if UNITY_EDITOR    
    [Button]
    public void LoadSampleData()
    {
        _researchModeData.LoadSampleData(Camera.main.transform.position);
    }
#endif
    
    void Start()
    {
        _mediaMaterial = Preview.GetComponent<MeshRenderer>().material;
        _researchModeData = new ObservableResearchModeData();

        _researchModeData.CenterDistance.Debug("Center distance").Subscribe();

        DepthSensorToggle.OnClick.AddListener(HandleDepthSensorToggle);
        
        PreviewToggle.ObserveIsToggled().Subscribe(b =>
        {
            if (b)
                _previewSubscription = _researchModeData.DepthMapTexture.Subscribe(HandleDepthMapTextureReceived);
            else
                _previewSubscription?.Dispose();
            
            Preview.SetActive(b);
        }).AddTo(this);

        ContinuousToggle.ObserveIsToggled().Subscribe(b =>
        {
            if (b)
                _pointCloudSubscription = _researchModeData.PointCloud.Subscribe(HandlePointCloudReceived);
            else
                _pointCloudSubscription?.Dispose();
        }).AddTo(this);
        
        _researchModeData.CenterDistance.SubscribeToText(Text, f => f.ToString("F4"));
        ShowPointCloudToggle.OnClick.AddListener(() => PointCloudVisualizer.ShowPointCloud = ShowPointCloudToggle.IsToggled);
        
    }

    private void HandlePointCloudReceived((float[] points, Vector3 center) data)
    {
        if (data.points == null)
            return;

        PointCloudVisualizer.SetParticles(data.points, data.center);
        PointCloudVisualizer.CalculateQuad(data.center);
    }

    private void HandleDepthMapTextureReceived(byte[] map)
    {
        if (map == null || map.Length == 0)
            return;

        if (!_mediaTexture)
        {
            _mediaTexture = new Texture2D(512, 512, TextureFormat.Alpha8, false);
            _mediaMaterial.mainTexture = _mediaTexture;
        }
        
        _mediaTexture.LoadRawTextureData(map);
        _mediaTexture.Apply();
    }

    private void HandleDepthSensorToggle()
    {
        if (DepthSensorToggle.IsToggled)
        {
            _researchModeData.Start();
        }
        else
        {
            _researchModeData.Stop();
        }
    }
}
