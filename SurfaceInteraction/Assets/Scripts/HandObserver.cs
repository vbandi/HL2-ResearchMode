using System;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UniRx;

namespace DefaultNamespace
{
    public class HandObserver
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
    }
}