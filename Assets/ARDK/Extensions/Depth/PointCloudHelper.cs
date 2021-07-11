// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Niantic.ARDK.AR;
using Niantic.ARDK.AR.ARSessionEventArgs;

using UnityEngine;
using UnityEngine.Rendering;

namespace Niantic.ARDK.Extensions.Depth
{
    /// This helper can be placed in a scene to help visualize the depth point cloud.
    /// All data is expected to come from ARSession.
    public class PointCloudHelper: MonoBehaviour
    {
        private const int MAX_SIMULTANEOUS_DRAW = 1023;

        private IARSession _session;

        private bool _drawPointCloud;
        private Vector3[] _pointCloud;
        private Matrix4x4[] _pointCloudMatrices;

        [SerializeField]
        private GameObject _pointObject = null;

        [SerializeField]
        private float _pointObjectScale = 0.01f;

        [SerializeField]
        private int _pointCloudDrawSkipCount = 6;

        private Mesh _pointMesh;
        private Material _pointMaterial;

#region SessionBookkeeping
        private void Start()
        {
            ARSessionFactory.SessionInitialized += OnAnyDidInitialize;
        }

        private void OnDestroy()
        {
            ARSessionFactory.SessionInitialized -= OnAnyDidInitialize;
        }

        private void OnAnyDidInitialize(AnyARSessionInitializedArgs args)
        {
            if (_session != null)
                return;

            _session = args.Session;
            _session.FrameUpdated += OnFrameUpdated;
            _session.Deinitialized += OnSessionDeinitialized;
        }

        private void OnSessionDeinitialized(ARSessionDeinitializedArgs args)
        {
            if (_session == null)
                return;

            _session = null;
        }
#endregion SessionBookkeeping

        private void OnFrameUpdated(FrameUpdatedArgs args)
        {
            var frame = args.Frame;
            if (frame == null || frame.Depth == null || frame.DepthFeaturePoints == null)
                return;

            // If this is our first DisparityBuffer frame, set up our resources
            if (_pointMesh == null)
            {
                // Set up the point cloud resources
                _pointMesh = _pointObject.GetComponent<MeshFilter>().sharedMesh;
                _pointMaterial = _pointObject.GetComponent<Renderer>().sharedMaterial;
            }

            PreparePointCloud(frame);
        }

        private void Update()
        {
            if (!_drawPointCloud || _pointCloud == null)
                return;

            // Draw the cloud
            var numPointsToDraw = _pointCloudMatrices.Length;
            var matrixBuffer = new Matrix4x4[MAX_SIMULTANEOUS_DRAW];
            int drawLength;
            for (var i = 0; i < numPointsToDraw; i += drawLength)
            {
                drawLength = Math.Min(MAX_SIMULTANEOUS_DRAW, numPointsToDraw - i);

                System.Array.Copy(_pointCloudMatrices, i, matrixBuffer, 0, drawLength);
                Graphics.DrawMeshInstanced
                (
                    _pointMesh,
                    0,
                    _pointMaterial,
                    matrixBuffer,
                    drawLength,
                    null,
                    ShadowCastingMode.Off,
                    false
                );
            }
        }

        /// When enabled, the next point cloud is saved and rendered until drawing is disabled.
        public void ToggleDrawPointCloud()
        {
            _drawPointCloud = !_drawPointCloud;

            if (!_drawPointCloud)
                _pointCloud = null;
        }

        private void PreparePointCloud(IARFrame frame)
        {
            if (!_drawPointCloud || _pointCloud != null)
                return;

            var numPoints = frame.DepthFeaturePoints.Points.Count;
            var numPointsToDraw = numPoints / _pointCloudDrawSkipCount;

            // If we haven't made our particle matrix array yet, do so now
            if (_pointCloudMatrices == null)
                _pointCloudMatrices = new Matrix4x4[numPointsToDraw];

            _pointCloud = frame.DepthFeaturePoints.Points.ToArray();

            for (var i = 0; i < numPointsToDraw; ++i)
            {
                _pointCloudMatrices[i] = Matrix4x4.TRS
                (
                    _pointCloud[i * _pointCloudDrawSkipCount],
                    Quaternion.identity,
                    Vector3.one * _pointObjectScale
                );
            }
        }
    }
}