using System;
using System.Diagnostics;
using System.Timers;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UniRx;
using UnityEngine;

public class Ruler : MonoBehaviour
{
    private Vector3 _from;
    private  Vector3 _to;
    private Plane _plane;

    private LineRenderer _line;
    public TextMeshPro Label;
    public double MinDistance  = 0.01;

    [Space(10)]
    public Transform FromButtons;
    
    public Interactable FromLeftButton;
    public Interactable FromRightButton;
    public Interactable FromUpButton;
    public Interactable FromDownButton;

    [Space(10)]
    public Transform ToButtons;
    public Interactable ToLeftButton;
    public Interactable ToRightButton;
    public Interactable ToUpButton;
    public Interactable ToDownButton;
    public float ButtonMovement = 0.01f;

    [HideInInspector]
    public bool ShowButtons = false;
    
    private void Start()
    {
        _line = GetComponent<LineRenderer>();
        MessageBroker.Default.Receive<ClearAllMessage>().Subscribe(_ => Clear());

        FromLeftButton.OnClick.AddListener(() =>
        {
            var direction = _to - _from;
            var f = _from - direction.normalized * ButtonMovement;
            SetPoints(f, _to, _plane);
        });
        
        FromRightButton.OnClick.AddListener(() =>
        {
            var direction = _to - _from;
            var f = _from + direction.normalized * ButtonMovement;
            SetPoints(f, _to, _plane);
        });
        
        FromUpButton.OnClick.AddListener(() =>
        {
            var direction = _to - _from;
            var cross = Vector3.Cross(direction, _plane.normal);
            var f = _from - cross.normalized * ButtonMovement;
            SetPoints(f, _to, _plane);
        });   
        
        FromDownButton.OnClick.AddListener(() =>
        {
            var direction = _to - _from;
            var cross = Vector3.Cross(direction, _plane.normal);
            var f = _from + cross.normalized * ButtonMovement;
            SetPoints(f, _to, _plane);
        });
        
        ToLeftButton.OnClick.AddListener(() =>
        {
            var direction = _to - _from;
            var t = _to + direction.normalized * ButtonMovement;
            SetPoints(_from, t, _plane);
        });
        
        ToRightButton.OnClick.AddListener(() =>
        {
            var direction = _to - _from;
            var t = _to - direction.normalized * ButtonMovement;
            SetPoints(_from, t, _plane);
        });
        
        ToUpButton.OnClick.AddListener(() =>
        {
            var direction = _to - _from;
            var cross = Vector3.Cross(direction, _plane.normal);
            var t = _to + cross.normalized * ButtonMovement;
            SetPoints(_from, t, _plane);
        });   
        
        ToDownButton.OnClick.AddListener(() =>
        {
            var direction = _to - _from;
            var cross = Vector3.Cross(direction, _plane.normal);
            var t = _to - cross.normalized * ButtonMovement;
            SetPoints(_from, t, _plane);
        });

        this.ObserveEveryValueChanged(x => x._to).Subscribe(_ => lastToUpdate = Time.timeSinceLevelLoad);
    }

    private float lastToUpdate = 0;

    private void Update()
    {
        if (Time.timeSinceLevelLoad - lastToUpdate > 2)
            ShowButtons = true;

        FromButtons.gameObject.SetActive(ShowButtons);
        ToButtons.gameObject.SetActive(ShowButtons);
    }

    public void SetPoints(Vector3 from, Vector3 to, Plane plane)
    {
        _plane = plane;
        _from = from;
        _to = to;
        
        var distance = Vector3.Distance(_from, _to);

        if (distance < MinDistance)
        {
            _line.enabled = false;
            Label.enabled = false;
            ShowButtons = false;
            return;
        }

        _line.enabled = true;
        Label.enabled = true;
        
        _line.SetPositions(new []{_from, _to});
        
        Label.transform.position = _from + (plane.normal * 0.05f);
        Label.text = (distance * 100).ToString("F1");

        FromButtons.SetPositionAndRotation(_from, Quaternion.LookRotation(_to - _from, plane.normal));
        ToButtons.SetPositionAndRotation(_to, Quaternion.LookRotation(_from - _to, plane.normal));

    }
    
    public void Clear()
    {
        _from = Vector3.zero;
        _to = Vector3.zero;
        _line.enabled = false;
        Label.text = "";
    }
}
