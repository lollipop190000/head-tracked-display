using System;
using UnityEngine;

namespace HeadTracked.Display
{
    /// <summary>Scene construction parameters. Lengths are metres, not projection multipliers.</summary>
    [Serializable]
    public sealed class BillboardIllusionSettings
    {
        [Range(.4f, .9f)] public float openingWidthFraction = .76f;
        [Range(.4f, .9f)] public float openingHeightFraction = .70f;
        [Min(.003f)] public float frameWidth = .008f;
        [Min(.002f)] public float wallThickness = .006f;
        [Range(.08f, .8f)] public float boxDepth = .22f;
        [Range(.02f, .2f)] public float contentHeight = .08f;
        [Tooltip("Bounds centre, relative to the physical screen. Positive is behind the screen.")]
        [Range(-.1f, .5f)] public float staticDepth = -.035f;
        [Tooltip("Horizontal offset as a fraction of opening width. Near an edge makes frame occlusion visible.")]
        [Range(-.4f, .4f)] public float horizontalOffsetFraction = .35f;
        public bool showFrame = true;
        public bool showDepthReferences = true;
        public bool animate;
        [Range(.02f, .5f)] public float animationInsideDepth = .12f;
        [Range(.005f, .1f)] public float animationProtrusion = .035f;
        [Range(2f, 20f)] public float animationPeriodSeconds = 6f;

        public void Validate()
        {
            openingWidthFraction = Clamp(openingWidthFraction, .4f, .9f, .76f);
            openingHeightFraction = Clamp(openingHeightFraction, .4f, .9f, .70f);
            frameWidth = Clamp(frameWidth, .003f, .03f, .008f);
            wallThickness = Clamp(wallThickness, .002f, .03f, .006f);
            boxDepth = Clamp(boxDepth, .08f, .8f, .22f);
            contentHeight = Clamp(contentHeight, .02f, .2f, .08f);
            staticDepth = Clamp(staticDepth, -.1f, .5f, -.035f);
            horizontalOffsetFraction = Clamp(horizontalOffsetFraction, -.4f, .4f, .35f);
            animationInsideDepth = Clamp(animationInsideDepth, .02f, .5f, .12f);
            animationProtrusion = Clamp(animationProtrusion, .005f, .1f, .035f);
            animationPeriodSeconds = Clamp(animationPeriodSeconds, 2f, 20f, 6f);
        }

        private static float Clamp(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }
}
