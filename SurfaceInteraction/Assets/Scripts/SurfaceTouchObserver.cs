using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Rendering;
using Microsoft.MixedReality.Toolkit.Utilities;
using UniRx;
using UnityEngine;

public class SurfaceTouchObserver : MonoBehaviour
{
    private IObservable<MixedRealityPose?> _observableIndexTipPose;
    private IObservable<MixedRealityPose?> _observableIndexMiddleJointPose;

    private int _spatialAwarenessLayerId;

    private LineRenderer _line;
    public float PalmDistanceFromSurfaceForMouseToAppear;
    public float PalmDistanceFromSurfaceForGrab;

    public GameObject Mouse;
    public GameObject LeftButton;
    public GameObject Pointer;

    public Ruler Ruler;
    
    private Plane _meshPlane;
    public float MouseDownPalmDistanceThreshold = 0.01f;

    private List<Vector3> points = new List<Vector3>();

    private VirtualMouseStatus _mouseStatus = VirtualMouseStatus.None; 

    // Start is called before the first frame update
    private void Start()
    {
        _observableIndexTipPose = JointToObservable(Handedness.Right, TrackedHandJoint.IndexTip);
        _observableIndexMiddleJointPose = JointToObservable(Handedness.Right, TrackedHandJoint.IndexMiddleJoint);

        _spatialAwarenessLayerId = LayerMask.GetMask("Spatial Awareness");

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
            out var raycastResult, PalmDistanceFromSurfaceForMouseToAppear, _spatialAwarenessLayerId);

        var isGrabbed = raycastResult.distance < PalmDistanceFromSurfaceForGrab;
        
        var thumbDistance = palmPlane.GetDistanceToPoint(thumbTipPose.Position);
        var indexDistance = palmPlane.GetDistanceToPoint(indexTipPose.Position);
        var middleDistance = palmPlane.GetDistanceToPoint(middleTipPose.Position);
        var ringDistance = palmPlane.GetDistanceToPoint(ringTipPose.Position);
        var pinkyDistance = palmPlane.GetDistanceToPoint(pinkyTipPose.Position);

        bool isMouseDown = indexDistance < -MouseDownPalmDistanceThreshold;

        switch (_mouseStatus)
        {
            case VirtualMouseStatus.None:
                if (hasHit)
                    _mouseStatus = VirtualMouseStatus.Visible;
                
                Mouse.SetActive(hasHit);
                break;
            case VirtualMouseStatus.Visible:
                if (!hasHit)
                    _mouseStatus = VirtualMouseStatus.None;
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
                    points.Add(Pointer.transform.position);
                    _line.positionCount = points.Count;
                    _line.SetPositions(points.ToArray());

                    if (points.Any())
                    {
                        Ruler.From = points.First();
                        Ruler.To = points.Last();
                    }
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
        Mouse.transform.position = _meshPlane.ClosestPointOnPlane(palmPose.Position);
        
        if (setRotation)
        {
            Mouse.transform.LookAt(
                Mouse.transform.position + Vector3.ProjectOnPlane(palmPose.Forward, _meshPlane.normal),
                _meshPlane.normal);
        }
    }


    private Subject<MixedRealityPose?> JointToObservable(Handedness handedness, TrackedHandJoint joint)
    {
        var result = new Subject<MixedRealityPose?>();

        var subscription = Observable.EveryUpdate().Subscribe(_ =>
        {
            var hand = HandJointUtils.FindHand(handedness);

            if (hand != null && hand.TryGetJoint(joint, out var pose))
            {
                result.OnNext(pose);
            }
            else
            {
                result.OnNext(null);
            }
        });

        result.DoOnTerminate(subscription.Dispose);
        return result;
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