using System;
using System.Collections.Generic;
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
    
    private Plane _meshPlane;
    public float MouseDownPalmDistanceThreshold = 0.01f;

    private List<Vector3> points = new List<Vector3>(); 

    // Start is called before the first frame update
    private void Start()
    {
        _observableIndexTipPose = JointToObservable(Handedness.Right, TrackedHandJoint.IndexTip);
        _observableIndexMiddleJointPose = JointToObservable(Handedness.Right, TrackedHandJoint.IndexMiddleJoint);

        _spatialAwarenessLayerId = LayerMask.GetMask("Spatial Awareness");

        _line = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        //HandPoseUtils.finger curl doesn't work well with vertical hands
        //Debug.Log($"Index finger curl: {HandPoseUtils.IndexFingerCurl(Handedness.Right)}");

        var rightHand = HandJointUtils.FindHand(Handedness.Right);

        if (rightHand == null || rightHand.TrackingState != TrackingState.Tracked)
        {
            Mouse.SetActive(false);
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
        
        // Calculate distances. Assume palm normal is more or less perpendicular to surface.
        
        // Step 1: cast ray from palm DOWN, and find intersection with spatial mesh
        var hasHit = Physics.Raycast(palmPose.Position, -palmPose.Up,
            out var raycastResult, PalmDistanceFromSurfaceForMouseToAppear, _spatialAwarenessLayerId);
        
        Mouse.SetActive(hasHit);
        
        if (!hasHit)
            return;
        
        // Step 2: check if mouse is grabbed and button is "pressed"

        var thumbDistance = palmPlane.GetDistanceToPoint(thumbTipPose.Position);
        var indexDistance = palmPlane.GetDistanceToPoint(indexTipPose.Position);
        var middleDistance = palmPlane.GetDistanceToPoint(middleTipPose.Position);
        var ringDistance = palmPlane.GetDistanceToPoint(ringTipPose.Position);
        var pinkyDistance = palmPlane.GetDistanceToPoint(pinkyTipPose.Position);

        var isGrabbed = raycastResult.distance < PalmDistanceFromSurfaceForGrab;
        
        Pointer.SetActive(isGrabbed);
        
        if (!isGrabbed)
            _meshPlane = new Plane(raycastResult.normal, raycastResult.point);
        
        bool isMouseDown = isGrabbed && indexDistance < -MouseDownPalmDistanceThreshold;

        LeftButton.EnsureComponent<MaterialInstance>().Material.color = isMouseDown ? Color.white : Color.gray;
            
        // Step 3: identify spatial mesh plane at the above intersection
        if (isMouseDown)
        {
            points.Add(Pointer.transform.position);
        }
        else
        {
            points.Clear();
        }

        Mouse.transform.position = _meshPlane.ClosestPointOnPlane(palmPose.Position);
        
        // TODO: doesn't work on vertical surfaces
        Mouse.transform.LookAt(Mouse.transform.position + Vector3.ProjectOnPlane(palmPose.Forward, _meshPlane.normal));  
        
        // Step 3: get distance between index finger and spatial mesh plane above
        _line.positionCount = points.Count;
        _line.SetPositions(points.ToArray());
        
        // Debug.Log($"Palm-Surface: {PalmDistanceFromSurface}. IsGrabbed: {isGrabbed}, IsMouseDown: {isMouseDown}");
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
}