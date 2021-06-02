using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DefaultNamespace
{
    [RequireComponent(typeof(ParticleSystem))]
    public class PointCloudVisualizer : MonoBehaviour
    {
        private ParticleSystem _particleSystem;

        public Color PointColor = Color.yellow;
        public Color ClosestPointColor = Color.white;
        public Color CenterPointColor = Color.green;
        public float Size = 0.25f;
        public float NormalSize = 0.1f;
        public float minTriangleSize = 0.01f;

        public int numberOfTrianglesForNormal = 200;

        public Transform Quad;

        public bool ShowPointCloud;

        public Vector3? Normal;
        
        private ParticleSystem.Particle[] _particles;
        // private HashSet<Vector3> _closestPoints;
        
        public Vector3 MiddlePoint;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        public void ShowPoints()
        {
            _particleSystem.SetParticles(_particles);
        }

        public void HidePoints()
        {
            _particleSystem.Clear();
        }

        public void SetParticles(Vector3[] pointCloud, Vector3 middlePoint)
        {
            var size = pointCloud.Length;

            if (Application.isEditor)
                Debug.Log($"Number of particles in cloud: {size}");
            
            Array.Resize(ref _particles, size);

            // _closestPoints = new HashSet<Vector3>(
            //     GetClosestPoints(middlePoint, pointCloud, numberOfClosesPointsForNormalCalculation));

            for (int i = 0; i < size; i++)
            {
                var particle = _particles[i];
                particle.position = pointCloud[i];
                // particle.startColor = _closestPoints.Contains(particle.position) ? ClosestPointColor : PointColor;
                particle.startColor = PointColor;

                if (particle.position == middlePoint)
                    particle.startColor = CenterPointColor;
                
                particle.startSize = Size;

                _particles[i] = particle;
            }

            if (ShowPointCloud)
                _particleSystem.SetParticles(_particles);
            else
                _particleSystem.Clear();
        }

        private IEnumerable<Vector3> GetClosestPoints(Vector3 middle, float[] points, int count)
        {
            var size = points.Length / 3;

            var comparer = new DistanceComparer(middle);
            var result = new SortedSet<Vector3>(comparer);  // TODO: orderby is much faster. Or KDTree is even better

            for (int i = 0; i < size; i++)
            {
                int xIndex = i * 3;
                result.Add(new Vector3(points[xIndex], points[xIndex + 1], points[xIndex + 2]));  
            }

            return result.Take(count);
        }
    }

}