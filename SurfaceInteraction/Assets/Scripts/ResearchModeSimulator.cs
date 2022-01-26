using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// When running in the editor, this class injects depth sensor data to <see cref="ObservableResearchModeData"/>
    /// based on the "EmulatedWorldRoot" node
    /// </summary>
    /// <remarks>Only emulates CenterDistance, Center and SurfaceNormal for now</remarks>
    public class ResearchModeSimulator : MonoBehaviour
    {
        [Tooltip("The layers that the simulated sensors can see")]
        public LayerMask SensorLayers;
        
        [Tooltip("Frequency of simulated sensor data (Hz)")]
        public float SimulationFrequency = 5f;
        
        private float _lastSimulationTime;
        
        private ObservableResearchModeData _researchModeData;

        private void Start()
        {
            _researchModeData = ObservableResearchModeData.Instance;
        }

        private void Update()
        {
            if (!Application.isEditor)
                return;

            if (Time.time - _lastSimulationTime < 1f / SimulationFrequency)
                return;
            
            _lastSimulationTime = Time.time;
            
            var distance = 10f;
            
            // collide with the emulated world from the camera's center
            var cameraTransform = Camera.main.transform;
            
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out var hitInfo, distance, SensorLayers))
            {
                var hitPosition = hitInfo.point;
                
                _researchModeData.CenterDistance.OnNext((cameraTransform.position - hitPosition).magnitude);
                _researchModeData.Center.OnNext(hitPosition);
                _researchModeData.SurfaceNormal.OnNext((hitPosition, hitInfo.normal));
            }
        }
    }
}