using System;
using System.Linq;
using DefaultNamespace;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Rendering;
using Microsoft.MixedReality.Toolkit.Utilities;
using UniRx;
using UnityEngine;

public class VirtualMouseController : SurfaceDrawerBase
{
    private IObservable<MixedRealityPose?> _observableIndexTipPose;
    private IObservable<MixedRealityPose?> _observableIndexMiddleJointPose;

    public float PalmDistanceFromSurfaceForMouseToAppear;
    public float PalmDistanceFromSurfaceForGrab;
    public float MouseDownSurfaceDistanceThreshold = 0.015f;

    public GameObject Mouse;
    public GameObject LeftButton;
    public GameObject Pointer;

    private Plane? _meshPlane;

    private VirtualMouseStatus _mouseStatus = VirtualMouseStatus.None; 
    

    // Start is called before the first frame update
    private void Start()
    {
        _observableIndexTipPose = HandObserver.JointToObservable(Handedness.Right, TrackedHandJoint.IndexTip);
        _observableIndexMiddleJointPose = HandObserver.JointToObservable(Handedness.Right, TrackedHandJoint.IndexMiddleJoint);

        _line = GetComponent<LineRenderer>();

        this.ObserveEveryValueChanged(x => x._mouseStatus).Subscribe(s => Debug.Log($"Mouse Status: {s}"));

        MessageBroker.Default.Receive<ClearAllMessage>().Subscribe(_ => Clear());
    }

    private void Update()
    {

        // Calculate distances. Assume palm normal is more or less perpendicular to surface.
        
        var rightHand = HandJointUtils.FindHand(Handedness.Right);

        if (rightHand == null || rightHand.TrackingState != TrackingState.Tracked)
        {
            Mouse.SetActive(false);
            _mouseStatus = VirtualMouseStatus.None;
            return;
        }

        if (!rightHand.TryGetJoint(TrackedHandJoint.IndexTip, out var indexTipPose) || 
            !rightHand.TryGetJoint(TrackedHandJoint.ThumbTip, out var thumbTipPose) ||
            !rightHand.TryGetJoint(TrackedHandJoint.MiddleTip, out var middleTipPose) ||
            !rightHand.TryGetJoint(TrackedHandJoint.RingTip, out var ringTipPose) || 
            !rightHand.TryGetJoint(TrackedHandJoint.PinkyTip, out var pinkyTipPose) || 
            !rightHand.TryGetJoint(TrackedHandJoint.Palm, out var palmPose)) 
            return;

        Plane palmPlane = new Plane(palmPose.Up, palmPose.Position);

        // Step 1: cast ray from palm DOWN, and find intersection with spatial mesh
        var hasHit = Physics.Raycast(palmPose.Position, -palmPose.Up,
            out var raycastResult, PalmDistanceFromSurfaceForMouseToAppear, spatialAwarenessLayerId);

        var isGrabbed = raycastResult.distance < PalmDistanceFromSurfaceForGrab;

        bool isMouseDown = _meshPlane.HasValue && _meshPlane.Value.GetDistanceToPoint(indexTipPose.Position) < MouseDownSurfaceDistanceThreshold;

        switch (_mouseStatus)
        {
            case VirtualMouseStatus.None:
                if (hasHit)
                    _mouseStatus = VirtualMouseStatus.Visible;
                
                Mouse.SetActive(hasHit);
                break;
            case VirtualMouseStatus.Visible:
                if (!hasHit)
                {
                    _meshPlane = null;
                    _mouseStatus = VirtualMouseStatus.None;
                }
                else
                {
                    _meshPlane = new Plane(raycastResult.normal, raycastResult.point);
                    SetMousePosition(palmPose, true);

                    if (isGrabbed)
                        _mouseStatus = VirtualMouseStatus.Grabbed;
                }
                break;
            case VirtualMouseStatus.Grabbed:
                SetMousePosition(palmPose, false);
                Pointer.SetActive(isGrabbed);

                if (!isGrabbed)
                {
                    _mouseStatus = VirtualMouseStatus.Visible;
                }

                if (isGrabbed && isMouseDown)
                    _mouseStatus = VirtualMouseStatus.ButtonPressed; 
                break;
            case VirtualMouseStatus.ButtonPressed:
                SetMousePosition(palmPose, false);
                
                // Step 3: identify spatial mesh plane at the above intersection
                if (isMouseDown)
                {
                    AddPoint(Pointer.transform.position, _meshPlane.Value);
                }
                else
                {
                    points.Clear();
                    _mouseStatus = VirtualMouseStatus.Grabbed;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        LeftButton.EnsureComponent<MaterialInstance>().Material.color = 
            _mouseStatus == VirtualMouseStatus.ButtonPressed ? Color.white : Color.gray;

        PointerUtils.SetHandRayPointerBehavior(_mouseStatus == VirtualMouseStatus.None
            ? PointerBehavior.Default
            : PointerBehavior.AlwaysOff);
    }

    private void SetMousePosition(MixedRealityPose palmPose, bool setRotation)
    {
        Mouse.transform.position = _meshPlane.Value.ClosestPointOnPlane(palmPose.Position);
        
        if (setRotation)
        {
            Mouse.transform.LookAt(
                Mouse.transform.position + Vector3.ProjectOnPlane(palmPose.Forward, _meshPlane.Value.normal),
                _meshPlane.Value.normal);
        }
    }

    public void Clear()
    {
        points.Clear();
        _line.positionCount = 0;
        _line.SetPositions(points.ToArray());
    }
}

public enum VirtualMouseStatus
{
    None,
    Visible,
    Grabbed,
    ButtonPressed
}