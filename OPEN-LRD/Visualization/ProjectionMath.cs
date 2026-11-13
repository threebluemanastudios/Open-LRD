using System;
using System.Numerics;

namespace OPEN_LRD.Visualization
{
    internal sealed class CameraView
    {
        private const float MinimumDirectionLength = 1e-6f;

        public CameraView(
            Vector3 position,
            Vector3 lookDirection,
            Vector3 upDirection,
            double fieldOfViewDegrees,
            double viewportWidth,
            double viewportHeight,
            double nearPlaneDistance,
            double farPlaneDistance)
        {
            ValidateVector(position, nameof(position));
            ValidateVector(lookDirection, nameof(lookDirection));
            ValidateVector(upDirection, nameof(upDirection));

            if (!double.IsFinite(fieldOfViewDegrees)
                || fieldOfViewDegrees <= 0d
                || fieldOfViewDegrees >= 180d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fieldOfViewDegrees),
                    fieldOfViewDegrees,
                    "Field of view must be between zero and 180 degrees.");
            }

            if (!double.IsFinite(viewportWidth) || viewportWidth <= 0d)
                throw new ArgumentOutOfRangeException(nameof(viewportWidth));

            if (!double.IsFinite(viewportHeight) || viewportHeight <= 0d)
                throw new ArgumentOutOfRangeException(nameof(viewportHeight));

            if (!double.IsFinite(nearPlaneDistance) || nearPlaneDistance <= 0d)
                throw new ArgumentOutOfRangeException(nameof(nearPlaneDistance));

            if (!double.IsFinite(farPlaneDistance) || farPlaneDistance <= nearPlaneDistance)
                throw new ArgumentOutOfRangeException(nameof(farPlaneDistance));

            Vector3 look = Vector3.Normalize(lookDirection);
            Vector3 right = Vector3.Cross(look, upDirection);

            if (right.Length() < MinimumDirectionLength)
                throw new ArgumentException("Camera up direction cannot be parallel to the look direction.", nameof(upDirection));

            right = Vector3.Normalize(right);
            Vector3 up = Vector3.Normalize(Vector3.Cross(right, look));

            Position = position;
            LookDirection = look;
            Right = right;
            Up = up;
            FieldOfViewDegrees = fieldOfViewDegrees;
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
            NearPlaneDistance = nearPlaneDistance;
            FarPlaneDistance = farPlaneDistance;
        }

        public Vector3 Position { get; }

        public Vector3 LookDirection { get; }

        public Vector3 Right { get; }

        public Vector3 Up { get; }

        public double FieldOfViewDegrees { get; }

        public double ViewportWidth { get; }

        public double ViewportHeight { get; }

        public double NearPlaneDistance { get; }

        public double FarPlaneDistance { get; }

        public double AspectRatio => ViewportWidth / ViewportHeight;

        public bool TryProject(Vector3 world, out Vector2 screen, out double depth)
        {
            Vector3 delta = world - Position;
            depth = Vector3.Dot(delta, LookDirection);

            if (!double.IsFinite(depth)
                || depth <= NearPlaneDistance
                || depth >= FarPlaneDistance)
            {
                screen = default;
                return false;
            }

            double tanHalfHorizontal = Math.Tan(FieldOfViewDegrees * Math.PI / 360.0);
            double tanHalfVertical = tanHalfHorizontal / AspectRatio;
            double horizontal = Vector3.Dot(delta, Right) / (depth * tanHalfHorizontal);
            double vertical = Vector3.Dot(delta, Up) / (depth * tanHalfVertical);

            double screenX = (horizontal * 0.5 + 0.5) * ViewportWidth;
            double screenY = (0.5 - vertical * 0.5) * ViewportHeight;

            if (!double.IsFinite(screenX) || !double.IsFinite(screenY))
            {
                screen = default;
                return false;
            }

            screen = new Vector2((float)screenX, (float)screenY);
            return true;
        }

        public void CreateRay(Vector2 screen, out Vector3 origin, out Vector3 direction)
        {
            if (!TryCreateRay(screen, out origin, out direction))
                throw new ArgumentOutOfRangeException(nameof(screen), "Screen point must be finite.");
        }

        public bool TryCreateRay(Vector2 screen, out Vector3 origin, out Vector3 direction)
        {
            origin = Position;

            if (!float.IsFinite(screen.X) || !float.IsFinite(screen.Y))
            {
                direction = default;
                return false;
            }

            double tanHalfHorizontal = Math.Tan(FieldOfViewDegrees * Math.PI / 360.0);
            double tanHalfVertical = tanHalfHorizontal / AspectRatio;
            double normalizedX = screen.X * 2.0 / ViewportWidth - 1.0;
            double normalizedY = 1.0 - screen.Y * 2.0 / ViewportHeight;

            Vector3 candidate = LookDirection
                + Right * (float)(normalizedX * tanHalfHorizontal)
                + Up * (float)(normalizedY * tanHalfVertical);

            if (candidate.Length() < MinimumDirectionLength)
            {
                direction = default;
                return false;
            }

            direction = Vector3.Normalize(candidate);
            return true;
        }

        private static void ValidateVector(Vector3 value, string parameterName)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
                throw new ArgumentOutOfRangeException(parameterName, "Vector components must be finite.");
        }
    }

    internal static class ProjectionMath
    {
        public static CameraView FrameBounds(
            CameraView camera,
            Vector3 center,
            float radius,
            double margin = 1.15)
        {
            ArgumentNullException.ThrowIfNull(camera);

            if (!float.IsFinite(radius) || radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));

            if (!double.IsFinite(margin) || margin <= 0d)
                throw new ArgumentOutOfRangeException(nameof(margin));

            double tanHalfHorizontal = Math.Tan(camera.FieldOfViewDegrees * Math.PI / 360.0);
            double tanHalfVertical = tanHalfHorizontal / camera.AspectRatio;
            double halfHorizontal = Math.Atan(tanHalfHorizontal);
            double halfVertical = Math.Atan(tanHalfVertical);
            double halfAngle = Math.Min(halfHorizontal, halfVertical);
            double distance = radius > 0f
                ? radius / Math.Sin(halfAngle) * margin
                : margin;
            Vector3 position = center - camera.LookDirection * (float)distance;
            double near = Math.Max(0.01, distance - radius * 2.0);
            double far = Math.Max(near + 1.0, distance + radius * 2.0 + 1.0);

            return new CameraView(
                position,
                camera.LookDirection,
                camera.Up,
                camera.FieldOfViewDegrees,
                camera.ViewportWidth,
                camera.ViewportHeight,
                near,
                far);
        }

        public static bool TryIntersectRayAabb(
            Vector3 origin,
            Vector3 direction,
            Vector3 minimum,
            Vector3 maximum,
            out float distance)
        {
            distance = 0f;

            if (minimum.X > maximum.X || minimum.Y > maximum.Y || minimum.Z > maximum.Z)
                throw new ArgumentException("AABB minimum must not exceed its maximum.");

            if (direction.LengthSquared() < 1e-12f)
                return false;

            float tMinimum = 0f;
            float tMaximum = float.PositiveInfinity;

            if (!UpdateSlab(origin.X, direction.X, minimum.X, maximum.X, ref tMinimum, ref tMaximum)
                || !UpdateSlab(origin.Y, direction.Y, minimum.Y, maximum.Y, ref tMinimum, ref tMaximum)
                || !UpdateSlab(origin.Z, direction.Z, minimum.Z, maximum.Z, ref tMinimum, ref tMaximum))
            {
                return false;
            }

            if (tMaximum < 0f || tMinimum > tMaximum)
                return false;

            distance = tMinimum;
            return true;
        }

        private static bool UpdateSlab(
            float origin,
            float direction,
            float minimum,
            float maximum,
            ref float tMinimum,
            ref float tMaximum)
        {
            if (Math.Abs(direction) < 1e-8f)
                return origin >= minimum && origin <= maximum;

            float first = (minimum - origin) / direction;
            float second = (maximum - origin) / direction;

            if (first > second)
                (first, second) = (second, first);

            tMinimum = Math.Max(tMinimum, first);
            tMaximum = Math.Min(tMaximum, second);
            return tMinimum <= tMaximum;
        }
    }
}
