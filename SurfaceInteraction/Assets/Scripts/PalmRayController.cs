using System;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using NaughtyAttributes;
using UniRx;
using Unity.Mathematics;
using UnityEngine;

namespace DefaultNamespace
{
    public class PalmRayController : SurfaceDrawerWithPointer, IMixedRealitySpeechHandler
    {
        private IObservable<MixedRealityPose?> _observablePalmPose;
        private Vector3 _handForwardOnMeshPlane;

        public float MaxPalmDistanceFromSurface = 0.2f;
        public float PinchThreshold = 0.1f;

        private Plane? _meshPlane;

        [BoxGroup("Filter")]
        public float Beta;
        [BoxGroup("Filter")]
        public float MinCutoff;

        private bool _drawingWithVoiceCommand = false;
        
        // private OneEuroFilter2 _filter; 
        
        private void Start()
        {
            _observablePalmPose = HandObserver.JointToObservable(Handedness.Right, TrackedHandJoint.Palm);
            _observablePalmPose.Where(_ => isActiveAndEnabled).Subscribe(HandlePalmPoseChanged);
            
            CoreServices.InputSystem.RegisterHandler<IMixedRealitySpeechHandler>(this);
            Init();
            // _filter = new OneEuroFilter2();
        }

        private void HandlePalmPoseChanged(MixedRealityPose? palmPose)
        {
            if (!palmPose.HasValue)
            {
                Pointer.SetActive(false);
                _meshPlane = null;
                return;
            }
            
            var isDrawing = IsPinched(Handedness.Left) || IsPinched(Handedness.Right) || _drawingWithVoiceCommand;

            if (!isDrawing)
            {
                var hasHit = Physics.Raycast(palmPose.Value.Position, -palmPose.Value.Up, out var hit,
                    MaxPalmDistanceFromSurface, spatialAwarenessLayerId);

                Pointer.SetActive(hasHit);

                if (!hasHit)
                    return;

                _meshPlane = new Plane(hit.normal, hit.point);
            }
            else
            {
                if (_meshPlane == null)
                    return;
            }
            
            var closestPoint = _meshPlane.Value.ClosestPointOnPlane(palmPose.Value.Position);

            _handForwardOnMeshPlane = closestPoint -
                                      _meshPlane.Value
                                          .ClosestPointOnPlane(palmPose.Value.Position + palmPose.Value.Forward)
                                          .normalized;

            // _filter.Beta = Beta;
            // _filter.MinCutoff = MinCutoff;

            // var planeOrigin = new GameObject();
            // planeOrigin.transform.SetPositionAndRotation(_meshPlane.Value.ClosestPointOnPlane(Vector3.zero), Quaternion.LookRotation(_meshPlane.Value.ClosestPointOnPlane(Vector3.forward), _meshPlane.Value.normal));
            //
            // var closestPointOnPlaneOrigin = planeOrigin.transform.InverseTransformPoint(closestPoint);
            // var pointOnPlane = new float2(closestPointOnPlaneOrigin.x, closestPointOnPlaneOrigin.z);
            // var filteredPointOnPlane = _filter.Step(Time.time, pointOnPlane);
            //
            // var filteredPoint = planeOrigin.transform.TransformPoint(new Vector3(filteredPointOnPlane.x,
            //     closestPointOnPlaneOrigin.y, filteredPointOnPlane.y));
            //
            // Pointer.transform.SetPositionAndRotation(new Vector3(filteredPoint.x, closestPoint.y, filteredPoint.y),
            //     Quaternion.LookRotation(_handForwardOnMeshPlane, _meshPlane.Value.normal));

            Pointer.transform.position = closestPoint;

            if (!isDrawing)
            {
                Pointer.transform.LookAt(
                    Pointer.transform.position + Vector3.ProjectOnPlane(palmPose.Value.Forward, _meshPlane.Value.normal),
                    _meshPlane.Value.normal);
            }
            
            Pointer.transform.Translate(PointerOffset, Space.Self);

            if (isDrawing)
                AddPoint(Pointer.transform.position, _meshPlane.Value);
            else
            {
                _points.Clear();
            }
            
//            Destroy(planeOrigin); // TODO don't do this every update
        }

        private bool IsPinched(Handedness hand)
        {
            return HandJointUtils.FindHand(hand)?.TrackingState == TrackingState.Tracked
                   && HandPoseUtils.CalculateIndexPinch(hand) > PinchThreshold;
        }

        /// <inheritdoc />
        public void OnSpeechKeywordRecognized(SpeechEventData eventData)
        {
            if (eventData.Command.Keyword == "Start Measurement")
            {
                Debug.Log("Start Measurement");
                _drawingWithVoiceCommand = true;
            }

            if (eventData.Command.Keyword == "Stop Measurement")
            {
                Debug.Log("Stop Measurement");
                _drawingWithVoiceCommand = false;
            }

        }
    }
    
    sealed class OneEuroFilter2
    {
        #region Public properties

        public float Beta { get; set; }
        public float MinCutoff { get; set; }

        #endregion

        #region Public step function

        public float2 Step(float t, float2 x)
        {
            var t_e = t - _prev.t;

            // Do nothing if the time difference is too small.
            if (t_e < 1e-5f) return _prev.x;

            var dx = (x - _prev.x) / t_e;
            var dx_res = math.lerp(_prev.dx, dx, Alpha(t_e, DCutOff));

            var cutoff = MinCutoff + Beta * math.length(dx_res);
            var x_res = math.lerp(_prev.x, x, Alpha(t_e, cutoff));

            _prev = (t, x_res, dx_res);

            return x_res;
        }

        #endregion

        #region Private class members

        const float DCutOff = 1.0f;

        static float Alpha(float t_e, float cutoff)
        {
            var r = 2 * math.PI * cutoff * t_e;
            return r / (r + 1);
        }

        #endregion

        #region Previous state variables as a tuple

        (float t, float2 x, float2 dx) _prev;

        #endregion
    }
}