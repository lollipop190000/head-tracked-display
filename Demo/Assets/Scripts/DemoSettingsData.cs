using System;
using HeadTracked.Display;
using UnityEngine;

namespace HeadTracked.Demo
{
    public sealed partial class DemoBootstrap
    {
        private sealed class SceneModel
        {
            public string label, heightCm, depthCm, xCm, yCm;
            public Transform root, plinth;
            public float x, supportY, height, depth, defaultHeight;
            public Vector3 defaultCentre;
        }

        [Serializable]
        private sealed class ModelSizeAndDepth
        {
            public float height, depth, x, y;
            public bool hasPosition;
        }

        [Serializable]
        private sealed class MonitorHint { public float widthCm, heightCm; }

        [Serializable]
        private sealed class ModelLayoutFile
        {
            public ModelSizeAndDepth[] models;
            public bool isolatePlant;
            public bool depthComparison;
            public bool materialStudy;
            public bool billboardMode;
            public int billboardModel;
            public BillboardIllusionSettings billboardSettings;
            public bool hasBillboardRendering, stableBillboardEdges, billboardContactAO;
            public float billboardRenderScale;
        }

        [Serializable]
        private sealed class IntrinsicsFile
        {
            public int imageWidth, imageHeight;
            public float focalXPixels, focalYPixels, principalXPixels, principalYPixels;
            public float k1, k2, k3, p1, p2, rmsReprojectionError;
        }
    }
}
