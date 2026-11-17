using System;
using System.Numerics;

namespace OPEN_LRD.Visualization
{
    /// <summary>Models camera position and orientation for first-person graph navigation.</summary>
    internal sealed class FlyCamera
    {
        public const double MaximumPitchRadians = 85.0 * Math.PI / 180.0;
        public const double DefaultLookSensitivity = 0.0025;
        private static readonly Vector3 LocalRight = -Vector3.UnitX;
        private Quaternion orientation = Quaternion.Identity;

        /// <summary>Creates a fly camera at the world origin looking along the positive Z axis.</summary>
        public FlyCamera()
            : this(Vector3.Zero, 0d, 0d)
        {
        }

        /// <summary>Creates a fly camera at the specified position, yaw, and pitch.</summary>
        public FlyCamera(Vector3 position, double yaw, double pitch)
        {
            SetPose(position, yaw, pitch);
        }

        public Vector3 Position { get; private set; }

        public double Yaw
        {
            get
            {
                Vector3 look = LookDirection;
                return Math.Atan2(look.X, look.Z);
            }
        }

        public double Pitch => Math.Asin(Math.Clamp(LookDirection.Y, -1f, 1f));

        public Vector3 LookDirection => Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, orientation));

        public Vector3 Right => Vector3.Normalize(Vector3.Transform(LocalRight, orientation));

        public Vector3 Up => Vector3.Normalize(Vector3.Transform(Vector3.UnitY, orientation));

        public Quaternion Orientation => orientation;

        /// <summary>Sets the camera position and yaw/pitch angles while clamping pitch within limits.</summary>
        public void SetPose(Vector3 position, double yaw, double pitch)
        {
            ValidateVector(position, nameof(position));

            if (!double.IsFinite(yaw))
                throw new ArgumentOutOfRangeException(nameof(yaw));

            if (!double.IsFinite(pitch))
                throw new ArgumentOutOfRangeException(nameof(pitch));

            Position = position;
            double boundedPitch = Math.Clamp(pitch, -MaximumPitchRadians, MaximumPitchRadians);
            Quaternion yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)yaw);
            Quaternion pitchRotation = Quaternion.CreateFromAxisAngle(LocalRight, (float)boundedPitch);
            orientation = Quaternion.Normalize(yawRotation * pitchRotation);
        }

        /// <summary>Sets the camera position and orthonormal orientation from look and up vectors.</summary>
        public void SetOrientation(Vector3 position, Vector3 lookDirection, Vector3 upDirection)
        {
            ValidateVector(position, nameof(position));
            ValidateVector(lookDirection, nameof(lookDirection));
            ValidateVector(upDirection, nameof(upDirection));

            if (lookDirection.LengthSquared() < 1e-12f)
                throw new ArgumentOutOfRangeException(nameof(lookDirection));

            Vector3 look = Vector3.Normalize(lookDirection);
            Vector3 right = Vector3.Cross(look, upDirection);
            if (right.LengthSquared() < 1e-12f)
                throw new ArgumentException("Camera up direction cannot be parallel to the look direction.", nameof(upDirection));

            right = Vector3.Normalize(right);
            Vector3 up = Vector3.Normalize(Vector3.Cross(right, look));
            Matrix4x4 basis = new Matrix4x4(
                -right.X, -right.Y, -right.Z, 0f,
                up.X, up.Y, up.Z, 0f,
                look.X, look.Y, look.Z, 0f,
                0f, 0f, 0f, 1f);

            Position = position;
            orientation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(basis));
        }

        /// <summary>Rotates the camera orientation in response to pointer movement deltas.</summary>
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

            float yawDelta = (float)(deltaX * sensitivity);
            float pitchDelta = (float)(-deltaY * sensitivity);
            Quaternion yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawDelta);
            Quaternion afterYaw = Quaternion.Normalize(yawRotation * orientation);

            float currentPitch = (float)Math.Asin(Math.Clamp(Vector3.Transform(Vector3.UnitZ, afterYaw).Y, -1f, 1f));
            float allowedPitch = ClampPitchInput(currentPitch, pitchDelta);
            Vector3 rightAfterYaw = Vector3.Normalize(Vector3.Transform(LocalRight, afterYaw));
            Quaternion pitchRotation = Quaternion.CreateFromAxisAngle(rightAfterYaw, allowedPitch);
            orientation = Quaternion.Normalize(pitchRotation * afterYaw);
        }

        /// <summary>Rolls the camera around its local forward axis by the given angle in radians.</summary>
        public void Roll(double deltaRadians)
        {
            ValidateFinite(deltaRadians, nameof(deltaRadians));
            Quaternion roll = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)deltaRadians);
            orientation = Quaternion.Normalize(orientation * roll);
        }

        /// <summary>Translates the camera along its forward, right, and world-up axes.</summary>
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

        /// <summary>Positions the camera along its current look axis to fit a bounding sphere in view.</summary>
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

        /// <summary>Orbits the camera position and orientation around a target point while respecting pitch limits.</summary>
        public void OrbitAroundTarget(Vector3 target, double deltaYawRadians, double deltaPitchRadians)
        {
            ValidateVector(target, nameof(target));

            if (!double.IsFinite(deltaYawRadians))
                throw new ArgumentOutOfRangeException(nameof(deltaYawRadians));

            if (!double.IsFinite(deltaPitchRadians))
                throw new ArgumentOutOfRangeException(nameof(deltaPitchRadians));

            Vector3 offset = Position - target;
            float radius = offset.Length();
            if (radius < 1e-4f)
                return;

            // Current spherical angles from target to camera
            double currentElevation = Math.Asin(Math.Clamp(offset.Y / radius, -1f, 1f));
            double currentAzimuth = Math.Atan2(offset.X, offset.Z);

            double boundedElevation = Math.Clamp(
                currentElevation + deltaPitchRadians,
                -MaximumPitchRadians,
                MaximumPitchRadians);
            double actualElevationDelta = boundedElevation - currentElevation;

            Quaternion yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)deltaYawRadians);
            double newAzimuth = currentAzimuth + deltaYawRadians;
            Vector3 elevationAxis = Vector3.Normalize(new Vector3(
                (float)-Math.Cos(newAzimuth),
                0f,
                (float)Math.Sin(newAzimuth)));

            Quaternion positionRotation = CreateOrbitRotation(yawRotation, elevationAxis, actualElevationDelta);
            if (!IsCameraPitchWithinLimits(positionRotation))
            {
                double low = 0d;
                double high = 1d;
                for (int iteration = 0; iteration < 24; iteration++)
                {
                    double fraction = (low + high) * 0.5d;
                    Quaternion candidateRotation = CreateOrbitRotation(
                        yawRotation,
                        elevationAxis,
                        actualElevationDelta * fraction);
                    if (IsCameraPitchWithinLimits(candidateRotation))
                        low = fraction;
                    else
                        high = fraction;
                }

                actualElevationDelta *= low;
                positionRotation = CreateOrbitRotation(yawRotation, elevationAxis, actualElevationDelta);
            }

            Vector3 newOffset = Vector3.Transform(offset, positionRotation);
            Position = target + newOffset;
            orientation = Quaternion.Normalize(positionRotation * orientation);
        }

        /// <summary>Checks whether applying a rotation keeps the camera pitch within allowed bounds.</summary>
        private bool IsCameraPitchWithinLimits(Quaternion positionRotation)
        {
            Quaternion candidateOrientation = Quaternion.Normalize(positionRotation * orientation);
            float lookY = Vector3.Transform(Vector3.UnitZ, candidateOrientation).Y;
            double pitch = Math.Asin(Math.Clamp(lookY, -1f, 1f));
            return pitch >= -MaximumPitchRadians - 1e-6
                && pitch <= MaximumPitchRadians + 1e-6;
        }

        /// <summary>Composes yaw and elevation rotations for an orbit step.</summary>
        private static Quaternion CreateOrbitRotation(
            Quaternion yawRotation,
            Vector3 elevationAxis,
            double elevationDelta)
        {
            Quaternion elevationRotation = Quaternion.CreateFromAxisAngle(elevationAxis, (float)elevationDelta);
            return Quaternion.Normalize(elevationRotation * yawRotation);
        }

        /// <summary>Clamps a requested pitch delta so the resulting pitch stays within bounds.</summary>
        private static float ClampPitchInput(float currentPitch, float requestedDelta)
        {
            float minimum = (float)-MaximumPitchRadians;
            float maximum = (float)MaximumPitchRadians;
            float requestedPitch = currentPitch + requestedDelta;

            if (currentPitch > maximum)
                return requestedDelta <= 0f ? Math.Max(requestedDelta, minimum - currentPitch) : 0f;
            if (currentPitch < minimum)
                return requestedDelta >= 0f ? Math.Min(requestedDelta, maximum - currentPitch) : 0f;

            return Math.Clamp(requestedPitch, minimum, maximum) - currentPitch;
        }

        /// <summary>Throws when the specified floating-point value is not finite.</summary>
        private static void ValidateFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }

        /// <summary>Throws when any component of the specified vector is not finite.</summary>
        private static void ValidateVector(Vector3 value, string parameterName)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
                throw new ArgumentOutOfRangeException(parameterName, "Vector components must be finite.");
        }
    }
}
