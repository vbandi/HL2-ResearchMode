using Microsoft.MixedReality.Toolkit.Input;
using UniRx;
using UnityEngine;

namespace DefaultNamespace
{
    public class SurfaceDrawerWithPointer : SurfaceDrawerBase
    {
        public GameObject Pointer;
        public Vector3 PointerOffset;

        protected void Init()
        {
            Observable.EveryUpdate().Where(_ => isActiveAndEnabled).Subscribe(_ =>
                PointerUtils.SetHandRayPointerBehavior(Pointer.activeInHierarchy
                    ? PointerBehavior.AlwaysOff
                    : PointerBehavior.Default));
            
            _line = GetComponent<LineRenderer>();
        }
    }
}