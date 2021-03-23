using System;
using DefaultNamespace;
using Microsoft.MixedReality.Toolkit.UI;
using NaughtyAttributes;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
   
    public Interactable DepthSensorToggle;
    public Interactable PreviewToggle;
    public Interactable ContinuousToggle;
    public Interactable ShowPointCloudToggle;
    public Interactable PointCloudSnapshotButton;

    public Interactable ShowNormalsToggle;
    public Interactable ShowQuadsToggle;
    public Interactable ClearNormalsAndQuadsButton;

    public Transform SurfaceIndicatorRoot;

    public GameObject NormalIndicator;
    public GameObject SurfaceIndicator;
    
    public Text Text;
    public GameObject Preview;

    public PointCloudVisualizer PointCloudVisualizer;

    private Material _mediaMaterial = null;
    private Texture2D _mediaTexture = null;

    private ObservableResearchModeData _researchModeData;
    private IDisposable _previewSubscription;
    private IDisposable _pointCloudSubscription;
    private IDisposable _showNormalsSubscription;
    private IDisposable _quadsSubscription;
    
#if UNITY_EDITOR    
    [Button]
    public void LoadSampleData()
    {
        _researchModeData.LoadSampleData(Camera.main.transform.position);
    }
#endif

    private void Start()
    {
        _mediaMaterial = Preview.GetComponent<MeshRenderer>().material;
        _researchModeData = new ObservableResearchModeData();

        _researchModeData.SurfaceQuadFactory = () => Instantiate(SurfaceIndicator).transform;

        DepthSensorToggle.ObserveIsToggled().Subscribe(HandleDepthSensorToggle);
        PreviewToggle.ObserveIsToggled().Subscribe(HandlePreviewToggled).AddTo(this);
        ContinuousToggle.ObserveIsToggled().Subscribe(HandleContinuousToggled).AddTo(this);
        ShowNormalsToggle.ObserveIsToggled().Subscribe(HandleShowNormalsToggled).AddTo(this);
        ShowQuadsToggle.ObserveIsToggled().Subscribe(HandleShowQuadsToggled).AddTo(this);
        ShowPointCloudToggle.ObserveIsToggled().Subscribe(b => PointCloudVisualizer.ShowPointCloud = b).AddTo(this);
        
        _researchModeData.CenterDistance.SubscribeToText(Text, f => f.ToString("F4"));
        ClearNormalsAndQuadsButton.OnClick.AddListener(HandleClearNormalsAndQuadsClicked);
    }

    private void HandleShowNormalsToggled(bool b)
    {
        if (b)
        {
            _showNormalsSubscription = _researchModeData.SurfaceNormal.Subscribe(v =>
                {
                    var normalIndicator = Instantiate(NormalIndicator, SurfaceIndicatorRoot, true);
                    normalIndicator.transform.position = v.center;
                    normalIndicator.transform.LookAt(v.center + v.normal);
                }
            );
        }
        else
            _showNormalsSubscription?.Dispose();
    }
    
    private void HandleShowQuadsToggled(bool b)
    {
        if (b)
            _quadsSubscription = _researchModeData.SurfaceQuad.Subscribe(t =>
            {
                t.SetParent(SurfaceIndicatorRoot);
            });

        else
            _quadsSubscription?.Dispose();
    }

    private void HandleClearNormalsAndQuadsClicked()
    {
        for (var i = 0; i < SurfaceIndicatorRoot.childCount; i++)
            Destroy(SurfaceIndicatorRoot.GetChild(i).gameObject);

        MessageBroker.Default.Publish(new ClearAllMessage());
    }

    private void HandleContinuousToggled(bool b)
    {
        if (b)
            _pointCloudSubscription = _researchModeData.PointCloud.Subscribe(HandlePointCloudReceived);
        else
            _pointCloudSubscription?.Dispose();
    }

    private void HandlePreviewToggled(bool b)
    {
        if (b)
            _previewSubscription = _researchModeData.DepthMapTexture.Subscribe(HandleDepthMapTextureReceived);
        else
            _previewSubscription?.Dispose();

        Preview.SetActive(b);
    }

    private void HandlePointCloudReceived((Vector3[] points, Vector3 center) data)
    {
        PointCloudVisualizer.SetParticles(data.points, data.center);
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

    private void HandleDepthSensorToggle(bool b)
    {
        if (b)
        {
            _researchModeData.Start();
        }
        else
        {
            _researchModeData.Stop();
        }
    }
}
