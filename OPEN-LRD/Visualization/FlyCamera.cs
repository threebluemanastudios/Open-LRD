using System;
using System.Numerics;

namespace OPEN_LRD.Visualization
{
    internal sealed class FlyCamera
    {
        public const double MaximumPitchRadians = 85.0 * Math.PI / 180.0;
        public const double DefaultLookSensitivity = 0.0025;

        public FlyCamera()
            : this(Vector3.Zero, 0d, 0d)
        {
        }

        public FlyCamera(Vector3 position, double yaw, double pitch)
        {
            SetPose(position, yaw, pitch);
        }

        public Vector3 Position { get; private set; }

        public double Yaw { get; private set; }

        public double Pitch { get; private set; }

        public Vector3 LookDirection => new Vector3(
            (float)(Math.Cos(Pitch) * Math.Sin(Yaw)),
            (float)Math.Sin(Pitch),
            (float)(Math.Cos(Pitch) * Math.Cos(Yaw)));

        public Vector3 Right => Vector3.Normalize(Vector3.Cross(LookDirection, Vector3.UnitY));

        public Vector3 Up => Vector3.Normalize(Vector3.Cross(Right, LookDirection));

        public void SetPose(Vector3 position, double yaw, double pitch)
        {
            ValidateVector(position, nameof(position));

            if (!double.IsFinite(yaw))
                throw new ArgumentOutOfRangeException(nameof(yaw));

            if (!double.IsFinite(pitch))
                throw new ArgumentOutOfRangeException(nameof(pitch));

            Position = position;
            Yaw = yaw;
            Pitch = Math.Clamp(pitch, -MaximumPitchRadians, MaximumPitchRadians);
        }

        public void Look(
            double deltaX,
            double deltaY,
            double sensitivity = DefaultLookSensitivity)
        {
            if (!double.IsFinite(deltaX))
                throw new ArgumentOutOfRangeException(nameof(deltaX));

            if (!double.IsFinite(deltaY))
                throw new ArgumentOutOfRangeException(nameof(deltaY));

            if (!double.IsFinite(sensitivity) || sensitivity <= 0d)
                throw new ArgumentOutOfRangeException(nameof(sensitivity));

            Yaw += deltaX * sensitivity;
            Pitch = Math.Clamp(
                Pitch - deltaY * sensitivity,
                -MaximumPitchRadians,
                MaximumPitchRadians);
        }

        public void Move(
            double forward,
            double strafe,
            double vertical,
            double speed,
            double deltaSeconds)
        {
            ValidateFinite(forward, nameof(forward));
            ValidateFinite(strafe, nameof(strafe));
            ValidateFinite(vertical, nameof(vertical));

            if (!double.IsFinite(speed) || speed < 0d)
                throw new ArgumentOutOfRangeException(nameof(speed));

            if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            Vector3 movement = LookDirection * (float)forward
                + Right * (float)strafe
                + Vector3.UnitY * (float)vertical;

            if (movement.LengthSquared() > 1f)
                movement = Vector3.Normalize(movement);

            Position += movement * (float)(speed * deltaSeconds);
        }

        public void FrameSphere(
            Vector3 center,
            float radius,
            double fieldOfViewDegrees,
            double aspectRatio,
            double margin = 1.15)
        {
            ValidateVector(center, nameof(center));

            if (!float.IsFinite(radius) || radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));

            if (!double.IsFinite(fieldOfViewDegrees)
                || fieldOfViewDegrees <= 0d
                || fieldOfViewDegrees >= 180d)
            {
                throw new ArgumentOutOfRangeException(nameof(fieldOfViewDegrees));
            }

            if (!double.IsFinite(aspectRatio) || aspectRatio <= 0d)
                throw new ArgumentOutOfRangeException(nameof(aspectRatio));

            if (!double.IsFinite(margin) || margin <= 0d)
                throw new ArgumentOutOfRangeException(nameof(margin));

            double tanHalfHorizontal = Math.Tan(fieldOfViewDegrees * Math.PI / 360.0);
            double tanHalfVertical = tanHalfHorizontal / aspectRatio;
            double halfAngle = Math.Min(
                Math.Atan(tanHalfHorizontal),
                Math.Atan(tanHalfVertical));
            double distance = radius > 0f
                ? radius / Math.Sin(halfAngle) * margin
                : margin;

            Position = center - LookDirection * (float)distance;
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static void ValidateVector(Vector3 value, string parameterName)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
                throw new ArgumentOutOfRangeException(parameterName, "Vector components must be finite.");
        }
    }
}
