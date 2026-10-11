using System;
using NUnit.Framework;
using UnityEngine;

namespace HeadTracked.Display.Tests
{
    public sealed class PhysicalViewportTests
    {
        [TestCase(.30f, 0f)]
        [TestCase(.45f, .1f)]
        [TestCase(.60f, 1f)]
        [TestCase(.45f, 100f)]
        [TestCase(.45f, -.1f)]
        public void OffsetViewportMatchesIndependentPhysicalIntersection(float distance, float depth)
        {
            Rect surface = new Rect(.1f, .15f, .8f, .7f), camera = new Rect(.2f, .1f, .6f, .5f);
            Rect aperture = PhysicalScreenViewport.Calculate(.4f, .25f, surface, camera);
            var eye = new Vector3(.06f, -.025f, -distance);
            var point = new Vector3(.015f, .02f, depth);
            var projection = OffAxisProjection.Calculate(eye, aperture, .03f, 25000);
            Vector4 clip = projection * new Vector4(point.x - eye.x, point.y - eye.y, -(point.z - eye.z), 1);
            double hitX = (distance * (double)point.x + depth * (double)eye.x) / (distance + depth);
            double hitY = (distance * (double)point.y + depth * (double)eye.y) / (distance + depth);
            Assert.That(.5f + .5f * clip.x / clip.w, Is.EqualTo((hitX - aperture.xMin) / aperture.width).Within(1e-5));
            Assert.That(.5f + .5f * clip.y / clip.w, Is.EqualTo((hitY - aperture.yMin) / aperture.height).Within(1e-5));
        }

        [Test]
        public void OffCentreApertureCornersMapToTheirOwnEdgesWithoutMovingEye()
        {
            var aperture = new Rect(-.12f, -.035f, .18f, .08f);
            var eye = new Vector3(.06f, -.025f, -.45f);
            var projection = OffAxisProjection.Calculate(eye, aperture, .03f, 10);
            foreach (var corner in new[] { aperture.min, aperture.max })
            {
                Vector4 clip = projection * new Vector4(corner.x - eye.x, corner.y - eye.y, eye.z, 1);
                float edge = corner == aperture.min ? -1 : 1;
                Assert.That(clip.x / clip.w, Is.EqualTo(edge).Within(1e-5));
                Assert.That(clip.y / clip.w, Is.EqualTo(edge).Within(1e-5));
            }
        }

        [Test]
        public void WindowMoveChangesOnlyApertureAndFullViewportReturnsOriginalMatrix()
        {
            var eye = new Vector3(.08f, .02f, -.45f);
            var original = OffAxisProjection.Calculate(eye, .4f, .25f, .03f, 10);
            var full = PhysicalScreenViewport.Calculate(.4f, .25f, PhysicalScreenViewport.Full, PhysicalScreenViewport.Full);
            Assert.That(OffAxisProjection.Calculate(eye, full, .03f, 10), Is.EqualTo(original));
            var moved = PhysicalScreenViewport.Calculate(.4f, .25f, new Rect(.1f, .2f, .5f, .5f), PhysicalScreenViewport.Full);
            Assert.That(moved.center.x, Is.EqualTo(-.06f).Within(1e-6));
            Assert.That(moved.center.y, Is.EqualTo(-.0125f).Within(1e-6));
        }

        [TestCase(float.NaN, 0, 1, 1)]
        [TestCase(0, 0, float.PositiveInfinity, 1)]
        [TestCase(-.1f, 0, 1, 1)]
        [TestCase(0, 0, 0, 1)]
        [TestCase(.5f, 0, .6f, 1)]
        public void InvalidViewportIsRejected(float x, float y, float w, float h)
        {
            var rect = new Rect(x, y, w, h);
            Assert.That(PhysicalScreenViewport.IsValid(rect), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalScreenViewport.Calculate(.4f, .25f, rect, PhysicalScreenViewport.Full));
        }

        [Test]
        public void NonFiniteProjectionInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => OffAxisProjection.Calculate(new Vector3(float.NaN, 0, -.45f), .4f, .25f, .03f, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => OffAxisProjection.Calculate(new Vector3(0, 0, -.45f), .4f, .25f, .03f, float.PositiveInfinity));
        }
    }
}
