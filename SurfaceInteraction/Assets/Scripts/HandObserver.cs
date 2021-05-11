using System;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UniRx;
using UnityEngine;

namespace DefaultNamespace
{
    public static class HandObserver
    {
        public static IObservable<MixedRealityPose?> JointToObservable(Handedness handedness, TrackedHandJoint joint)
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

        public static IObservable<bool> PlaneTouched(Handedness handedness, TrackedHandJoint tipJoint, Plane? plane)
        {
            var tip = JointToObservable(handedness, tipJoint);
            var distal = JointToObservable(handedness, GetDistalJointForTip(tipJoint));

            return tip.CombineLatest(distal, (t, d) =>
            {
                if (t == null || d == null || plane == null)
                    return false;

                return !plane.Value.SameSide(t.Value.Position, d.Value.Position);
            });
        }

        private static TrackedHandJoint GetDistalJointForTip(TrackedHandJoint fingerTipJoint)
        {
            switch (fingerTipJoint)
            {
                case TrackedHandJoint.IndexTip: return TrackedHandJoint.IndexDistalJoint;
                case TrackedHandJoint.MiddleTip: return TrackedHandJoint.MiddleDistalJoint;
                case TrackedHandJoint.RingTip: return TrackedHandJoint.RingDistalJoint;
                case TrackedHandJoint.PinkyTip: return TrackedHandJoint.PinkyDistalJoint;
                case TrackedHandJoint.ThumbTip: return TrackedHandJoint.ThumbDistalJoint;

                default:
                    throw new ArgumentOutOfRangeException(nameof(fingerTipJoint), fingerTipJoint, "Not a finger tip joint");
            }
        }
    }
}