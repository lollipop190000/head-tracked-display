using System;
using UnityEngine;

namespace HeadTracked.Display
{
    /// <summary>Maps a render surface and camera rect to a calibrated display. Coordinates start at bottom left.</summary>
    public static class PhysicalScreenViewport
    {
        public static readonly Rect Full = new Rect(0, 0, 1, 1);

        public static bool IsValid(Rect rect) => Finite(rect.x) && Finite(rect.y) &&
            Finite(rect.width) && Finite(rect.height) && rect.width > 0 && rect.height > 0 &&
            rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= 1 && rect.yMax <= 1;

        public static Rect Calculate(float width, float height, Rect displayViewport, Rect cameraViewport)
        {
            if (!Finite(width) || !Finite(height) || width <= 0 || height <= 0 ||
                !IsValid(displayViewport) || !IsValid(cameraViewport))
                throw new ArgumentOutOfRangeException(nameof(displayViewport), "Invalid physical display or viewport.");
            float x = displayViewport.x + cameraViewport.x * displayViewport.width;
            float y = displayViewport.y + cameraViewport.y * displayViewport.height;
            return new Rect((x - .5f) * width, (y - .5f) * height,
                displayViewport.width * cameraViewport.width * width,
                displayViewport.height * cameraViewport.height * height);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
