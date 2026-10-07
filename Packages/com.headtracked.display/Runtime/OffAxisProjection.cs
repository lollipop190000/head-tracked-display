using UnityEngine;

namespace HeadTracked.Display
{
    public static class OffAxisProjection
    {
        /// <summary>Screen-local +Z points into the scene; eye.z must be negative.</summary>
        public static Matrix4x4 Calculate(Vector3 eye, float screenWidth, float screenHeight, float near, float far)
        {
            float distance = -eye.z;
            if (distance <= near || near <= 0f || far <= near || screenWidth <= 0f || screenHeight <= 0f)
                throw new System.ArgumentOutOfRangeException(nameof(eye), "Invalid eye, screen or clip plane geometry.");

            float scale = near / distance;
            float left = (-screenWidth * 0.5f - eye.x) * scale;
            float right = (screenWidth * 0.5f - eye.x) * scale;
            float bottom = (-screenHeight * 0.5f - eye.y) * scale;
            float top = (screenHeight * 0.5f - eye.y) * scale;
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
    }
}
