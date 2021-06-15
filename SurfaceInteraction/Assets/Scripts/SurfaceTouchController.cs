using System;
using DefaultNamespace;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UniRx;
using UniRx.Diagnostics;
using UnityEngine;

public class SurfaceTouchController : SurfaceDrawerWithPointer
{
    private IObservable<MixedRealityPose?> _observableIndexMiddleJointPose;


    public float TouchDistance = 0.005f;
    public float MaxMiddleJointDistanceFromSurface = 0.2f;
    private Plane? _meshPlane;
    

    // Start is called before the first frame update
    void Start()
    {
        Pointer.SetActive(false);
        Init();
        _observableIndexMiddleJointPose = HandObserver.JointToObservable(Handedness.Right, TrackedHandJoint.IndexMiddleJoint).Where(_ => isActiveAndEnabled);
        // _observableIndexMiddleJointPose.Debug("index middle joint").Subscribe();
        _observableIndexMiddleJointPose.Subscribe(HandlePoseChanged);
    }

    private void HandlePoseChanged(MixedRealityPose? pose)
    {
        if (!pose.HasValue)
        {
            Pointer.SetActive(false);
            _meshPlane = null;
            return;
        }
        
        var tipAvailable = HandJointUtils.TryGetJointPose(TrackedHandJoint.IndexTip, Handedness.Right, out var tip);
        var distalAvailable = HandJointUtils.TryGetJointPose(TrackedHandJoint.IndexDistalJoint, Handedness.Right, out var distal);

        if (!tipAvailable || !distalAvailable)
        {
            Pointer.SetActive(false);
            _meshPlane = null;
            return;
        }
        
        var hasHit = Physics.Raycast(pose.Value.Position, tip.Forward, out var hit,
            MaxMiddleJointDistanceFromSurface, spatialAwarenessLayerId);

        if (!hasHit)
        {
            Pointer.SetActive(false);
            _meshPlane = null;
            return;
        }
        
        // if (_meshPlane == null)
        // {
        //     Pointer.SetActive(hasHit);
        //     _meshPlane = new Plane(hit.normal, hit.point);
        // }
        // else
        {
            Pointer.SetActive(true);
            _meshPlane = new Plane(hit.normal, hit.point);
            
            var closestPoint = _meshPlane.Value.ClosestPointOnPlane(pose.Value.Position);
            Pointer.transform.position = closestPoint;
            
            Pointer.transform.Translate(PointerOffset, Space.Self);
            
            Pointer.transform.LookAt(
                Pointer.transform.position + Vector3.ProjectOnPlane(tip.Up, _meshPlane.Value.normal),
                _meshPlane.Value.normal);
            
            if (!_meshPlane.Value.SameSide(tip.Position, pose.Value.Position) || _meshPlane.Value.GetDistanceToPoint(tip.Position) < TouchDistance)
                AddPoint(Pointer.transform.position, _meshPlane.Value);
            else
                points.Clear();
        }
    }
}
