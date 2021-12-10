using System;
using DefaultNamespace;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using NaughtyAttributes;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    public Interactable SpatialMeshToggle;
    public Interactable MeshGeneratorToggle;
    public Interactable RulerToggle;
    public Interactable DepthSensorToggle;
    public Interactable PreviewToggle;
    public Interactable ContinuousToggle;
    public Interactable ShowPointCloudToggle;
    public Interactable PointCloudSnapshotButton;
    public Interactable LongThrowToggle;
    public Interactable ShowCenterPointToggle;

    public Interactable DrawModeSelectorButton;
    public Interactable ShowMeshToggle;

    public Interactable ShowNormalsToggle;
    public Interactable ShowQuadsToggle;
    public Interactable ClearNormalsAndQuadsButton;
    public Interactable ModeSelectorButton;

    public Transform SurfaceIndicatorRoot;

    public GameObject NormalIndicator;
    public GameObject SurfaceIndicator;
    public GameObject Ruler;
    public GameObject CenterPointIndicator;

    public Text Text;
    public GameObject Preview;
    public MeshGenerator _meshGenerator;

    public PointCloudVisualizer PointCloudVisualizer;
    
    public GameObject[] Modes;

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
        _researchModeData = ObservableResearchModeData.Instance;

        _researchModeData.SurfaceQuadFactory = () => Instantiate(SurfaceIndicator).transform;

        DepthSensorToggle.ObserveIsToggled().Subscribe(HandleDepthSensorToggle);
        PreviewToggle.ObserveIsToggled().Subscribe(HandlePreviewToggled).AddTo(this);
        ContinuousToggle.ObserveIsToggled().Subscribe(HandleContinuousToggled).AddTo(this);
        ShowNormalsToggle.ObserveIsToggled().Subscribe(HandleShowNormalsToggled).AddTo(this);
        ShowQuadsToggle.ObserveIsToggled().Subscribe(HandleShowQuadsToggled).AddTo(this);
        ShowPointCloudToggle.ObserveIsToggled().Subscribe(b => PointCloudVisualizer.ShowPointCloud = b).AddTo(this);
        MeshGeneratorToggle.ObserveIsToggled().Subscribe(b => _meshGenerator.gameObject.SetActive(b)).AddTo(this);
        ShowMeshToggle.ObserveIsToggled().Subscribe(b => _meshGenerator.GetComponent<MeshRenderer>().enabled = b).AddTo(this);
        RulerToggle.ObserveIsToggled().Subscribe(b => Ruler.SetActive(b)).AddTo(this);
        LongThrowToggle.ObserveIsToggled().Subscribe(b => _researchModeData.LongThrowMode.Value = b).AddTo(this);
        ShowCenterPointToggle.ObserveIsToggled().Subscribe(b => CenterPointIndicator.SetActive(b)).AddTo(this);
        
        _researchModeData.CenterDistance.SubscribeToText(Text, f => f.ToString("F4"));
        _researchModeData.Center.Subscribe(p => CenterPointIndicator.transform.position = p);
        ClearNormalsAndQuadsButton.OnClick.AddListener(HandleClearNormalsAndQuadsClicked);

        SpatialMeshToggle.ObserveIsToggled().Subscribe(HandleSpatialMeshToggled);

        Observable.EveryUpdate().Subscribe(_ =>
            _researchModeData.PauseSurfaceQuadCalculation = HandJointUtils.FindHand(Handedness.Any) != null);
        
        // Mode selector button
        ModeSelectorButton.NumOfDimensions = Modes.Length;

        foreach (GameObject mode in Modes)
            mode.SetActive(false);

        ModeSelectorButton.ObserveCurrentDimension().Select(x => Modes[x]).Pairwise().Subscribe(ChangeMode);
        ChangeMode(new Pair<GameObject>(null, Modes[0]));
        
        // Draw mode selector button
        var drawingModeNames = Enum.GetNames(typeof(DrawingMode));
        DrawModeSelectorButton.NumOfDimensions = drawingModeNames.Length;
        DrawModeSelectorButton.ObserveCurrentDimension().Subscribe(x => ChangeDrawingMode((DrawingMode) x)).AddTo(this);
    }

    private void ChangeDrawingMode(DrawingMode drawingMode)
    {
        foreach (var surfaceDrawerBase in FindObjectsOfType<SurfaceDrawerBase>())
            surfaceDrawerBase.DrawingMode = drawingMode;

        DrawModeSelectorButton.GetComponent<ButtonConfigHelper>().MainLabelText = drawingMode.ToString();
    }

    private void HandleSpatialMeshToggled(bool b)
    {
        if (b)
        {
            ShowQuadsToggle.IsToggled = false;
            ShowNormalsToggle.IsToggled = false;
            ShowPointCloudToggle.IsToggled = false;
            ContinuousToggle.IsToggled = false;
            PreviewToggle.IsToggled = false;
            DepthSensorToggle.IsToggled = false;
            HandleClearNormalsAndQuadsClicked();
            CoreServices.SpatialAwarenessSystem?.Enable();
        }
        else
        {
            CoreServices.SpatialAwarenessSystem?.Disable();
        }
    }

    private void ChangeMode(Pair<GameObject> pair)
    {
        if (!(pair.Previous is null))
            pair.Previous.SetActive(false);

        pair.Current.SetActive(true);
        ModeSelectorButton.GetComponent<ButtonConfigHelper>().MainLabelText = pair.Current.name;
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
            LongThrowToggle.IsEnabled = false;
            _researchModeData.Start();
        }
        else
        {
            LongThrowToggle.IsEnabled = true;
            _researchModeData.Stop();
        }
    }
}
