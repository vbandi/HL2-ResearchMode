using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MixedReality.Toolkit.UI;
using NaughtyAttributes;
using UnityEngine;
using Random = UnityEngine.Random;

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

        public int numberOfClosesPointsForNormalCalculation = 100;
        public int numberOfTrianglesForNormal = 200;

        public LineRenderer NormalLineRenderer;

        public Transform Quad;

        public bool ShowPointCloud;

        public Vector3? Normal;
        
        private ParticleSystem.Particle[] _particles;
        private HashSet<Vector3> _closestPoints;

        private Vector3 _initialCameraPosition;
        private Quaternion _initialCameraRotation;
        public Vector3 MiddlePoint;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();

            _initialCameraPosition = Camera.main.transform.position;
            _initialCameraRotation = Camera.main.transform.rotation;
        }

        [Button("Load Sample Data")]
        private void LoadSampleData()
        {
            var sampleParticleData = SampleData.PointCloud.Split(',').Select(s => float.Parse(s)).ToArray();
            int middleIndex = sampleParticleData.Length / 2 + 600;  //TODO: this is not how the middle is counted because there's a lot of variation between the number of points
            middleIndex -= middleIndex % 3;

            MiddlePoint = new Vector3(sampleParticleData[middleIndex], sampleParticleData[middleIndex + 1],
                sampleParticleData[middleIndex + 2]);
            
            SetParticles(sampleParticleData, MiddlePoint);
            transform.localPosition = _initialCameraPosition;
            transform.localRotation = _initialCameraRotation;
            
            CalculateQuad(MiddlePoint);
        }

        public void CalculateQuad(Vector3 middlePoint)
        {

            if (_closestPoints.Count < numberOfClosesPointsForNormalCalculation / 3)
                return;
            
            Normal = CalculateNormal(middlePoint, _closestPoints.ToArray());

            if (Normal != null)
            {
                DrawNormal(middlePoint, Normal.Value);

                Quad.position = middlePoint;
                Quad.LookAt(middlePoint - Normal.Value);
            }

            var sumx = 0f;
            var sumy = 0f;
            var sumz = 0f;

            foreach (var closestPoint in _closestPoints)
            {
                var point = Quad.InverseTransformPoint(closestPoint);
                sumx += point.x;
                sumy += point.y;
                sumz += point.z;
                // Debug.Log(point.z);
            }

            // Debug.Log($"avgx: {sumx / _closestPoints.Count}, avgy: {sumy / _closestPoints.Count}, avgz: {sumz / _closestPoints.Count}");

            Quad.Translate(0, 0, sumz / _closestPoints.Count * Quad.localScale.z, Space.Self);
        }

        public void ShowPoints()
        {
            _particleSystem.SetParticles(_particles);
        }

        public void HidePoints()
        {
            _particleSystem.Clear();
        }

        public void SetParticles(float[] pointCloud, Vector3 middlePoint)
        {
            var size = pointCloud.Length / 3;

            if (Application.isEditor)
                Debug.Log($"Number of particles in cloud: {size}");
            
            Array.Resize(ref _particles, size);

            _closestPoints = new HashSet<Vector3>(
                GetClosestPoints(middlePoint, pointCloud, numberOfClosesPointsForNormalCalculation));

            for (int i = 0; i < size; i++)
            {
                var particle = _particles[i];
                var xIndex = i * 3;
                particle.position = new Vector3(pointCloud[xIndex], pointCloud[xIndex + 1], pointCloud[xIndex + 2]);
                particle.startColor = _closestPoints.Contains(particle.position) ? ClosestPointColor : PointColor;

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
            var result = new SortedSet<Vector3>(comparer);

            for (int i = 0; i < size; i++)
            {
                int xIndex = i * 3;
                result.Add(new Vector3(points[xIndex], points[xIndex + 1], points[xIndex + 2]));
            }

            return result.Take(count);
        }
        

        public void DrawNormal(Vector3 start, Vector3 direction)
        {
            NormalLineRenderer.SetPositions(new[] {start, start + direction.normalized * NormalSize});
        }

        public Vector3 CalculateNormal(Vector3 center, Vector3[] points)
        {
            var result = new Vector3();

            var axis = Camera.main.transform.position - center;
            
            // get numberOfTrianglesForNormal number of triangles with a minimum angle and side size
            for (int i = 0; i < numberOfTrianglesForNormal; i++) 
            {
                var a = points[Random.Range(0, points.Length)];
                var b = points[Random.Range(0, points.Length)];
                var c = points[Random.Range(0, points.Length)];

                var angle = Vector3.SignedAngle(a - c, a - b, axis);

                // skip if triangle is not large enough
                if (angle < 30 || angle > 120 || (a - b).magnitude < minTriangleSize || (a - c).magnitude < minTriangleSize ||
                    (b - c).magnitude < minTriangleSize)
                {
                    i = i - 1;
                    continue;
                }

                result += NormalOfTriangle(a, b, c);
            }
            return result.normalized;
        }

        public Vector3 NormalOfTriangle(Vector3 pointA, Vector3 pointB, Vector3 pointC)
        {
            var v1 = pointC - pointA;
            var v2 = pointB - pointA;
            return Vector3.Cross(v1, v2).normalized;
        }
    }

    public class DistanceComparer : IComparer<Vector3>
    {
        private Vector3 _origin;
        public DistanceComparer(Vector3 origin)
        {
            _origin = origin;
        }

        public int Compare(Vector3 x, Vector3 y)
        {
            return (x - _origin).magnitude.CompareTo((y - _origin).magnitude);
        }
    }
}