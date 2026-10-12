using UnityEngine;

namespace HeadTracked.Display
{
    public static class OffAxisProjection
    {
        /// <summary>Screen-local +Z points into the scene; eye.z must be negative.</summary>
        public static Matrix4x4 Calculate(Vector3 eye, float screenWidth, float screenHeight, float near, float far)
            => Calculate(eye, new Rect(-screenWidth * .5f, -screenHeight * .5f, screenWidth, screenHeight), near, far);

        /// <summary>Asymmetric physical aperture in the original screen frame. Eye coordinates remain unchanged.</summary>
        public static Matrix4x4 Calculate(Vector3 eye, Rect screenRectMeters, float near, float far)
        {
            float distance = -eye.z;
            if (!Finite(eye.x) || !Finite(eye.y) || !Finite(eye.z) || !Finite(near) || !Finite(far) ||
                !Finite(screenRectMeters.x) || !Finite(screenRectMeters.y) || !Finite(screenRectMeters.width) || !Finite(screenRectMeters.height) ||
                distance <= near || near <= 0f || far <= near || screenRectMeters.width <= 0f || screenRectMeters.height <= 0f)
                throw new System.ArgumentOutOfRangeException(nameof(eye), "Invalid eye, screen or clip plane geometry.");

            float scale = near / distance;
            float left = (screenRectMeters.xMin - eye.x) * scale;
            float right = (screenRectMeters.xMax - eye.x) * scale;
            float bottom = (screenRectMeters.yMin - eye.y) * scale;
            float top = (screenRectMeters.yMax - eye.y) * scale;
            Matrix4x4 m = Matrix4x4.zero;
            m[0, 0] = 2f * near / (right - left);
            m[0, 2] = (right + left) / (right - left);
            m[1, 1] = 2f * near / (top - bottom);
            m[1, 2] = (top + bottom) / (top - bottom);
            m[2, 2] = -(far + near) / (far - near);
            m[2, 3] = -2f * far * near / (far - near);
            m[3, 2] = -1f;
            return m;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
