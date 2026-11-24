using System;
using System.Collections.Generic;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal readonly record struct Ray(Vector3 Origin, Vector3 Direction);

    internal readonly record struct Plane(Vector3 Normal, double Offset);

    internal readonly record struct Triangle(Vector3 A, Vector3 B, Vector3 C);

    internal readonly record struct Triangle2D(Vector2 A, Vector2 B, Vector2 C);

    internal readonly record struct BoundingBox(Vector3 Minimum, Vector3 Maximum);

    internal sealed record RayHit(bool Hit, double Distance, Vector3 Point, Vector3 Normal);

    internal sealed record BarycentricCoordinates(double First, double Second, double Third);

    internal static class Geometry3DAndGraphics
    {
        private const double Tolerance = 1e-10;

        /// <summary>
        /// Computes the 2D convex hull enclosing points.
        /// </summary>
        public static IReadOnlyList<Vector2> ConvexHull(IReadOnlyList<Vector2> points)
        {
            ValidatePoints(points, 1, nameof(points));
            List<Vector2> sorted = new List<Vector2>(points);
            sorted.Sort(ComparePoints);
            List<Vector2> unique = new List<Vector2>();
            foreach (Vector2 point in sorted)
            {
                if (unique.Count == 0 || !SamePoint(unique[^1], point))
                {
                    unique.Add(point);
                }
            }
            if (unique.Count <= 2)
            {
                return unique.AsReadOnly();
            }

            List<Vector2> lower = new List<Vector2>();
            foreach (Vector2 point in unique)
            {
                while (lower.Count >= 2 && Cross(lower[^1] - lower[^2], point - lower[^1]) <= Tolerance)
                {
                    lower.RemoveAt(lower.Count - 1);
                }
                lower.Add(point);
            }
            List<Vector2> upper = new List<Vector2>();
            for (int index = unique.Count - 1; index >= 0; index--)
            {
                Vector2 point = unique[index];
                while (upper.Count >= 2 && Cross(upper[^1] - upper[^2], point - upper[^1]) <= Tolerance)
                {
                    upper.RemoveAt(upper.Count - 1);
                }
                upper.Add(point);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower.AsReadOnly();
        }

        public static bool IsPointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            EnsureFinite(point, nameof(point));
            List<Vector2> vertices = NormalizePolygon(polygon, nameof(polygon));
            bool inside = false;
            for (int first = 0, second = vertices.Count - 1; first < vertices.Count; second = first++)
            {
                Vector2 a = vertices[second];
                Vector2 b = vertices[first];
                if (PointOnSegment(point, a, b))
                {
                    return true;
                }
                bool crosses = (a.Y > point.Y) != (b.Y > point.Y)
                    && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
                if (crosses)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public static bool SegmentIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 intersection)
        {
            EnsureFinite(a, nameof(a));
            EnsureFinite(b, nameof(b));
            EnsureFinite(c, nameof(c));
            EnsureFinite(d, nameof(d));
            Vector2 firstDirection = b - a;
            Vector2 secondDirection = d - c;
            double denominator = Cross(firstDirection, secondDirection);
            Vector2 between = c - a;
            if (Math.Abs(denominator) <= Tolerance)
            {
                if (Math.Abs(Cross(between, firstDirection)) > Tolerance)
                {
                    intersection = default;
                    return false;
                }
                double lengthSquared = Dot(firstDirection, firstDirection);
                if (lengthSquared <= Tolerance * Tolerance)
                {
                    if (PointOnSegment(a, c, d))
                    {
                        intersection = a;
                        return true;
                    }
                    intersection = default;
                    return false;
                }
                double firstT = Dot(c - a, firstDirection) / lengthSquared;
                double secondT = Dot(d - a, firstDirection) / lengthSquared;
                double overlapStart = Math.Max(0.0, Math.Min(firstT, secondT));
                double overlapEnd = Math.Min(1.0, Math.Max(firstT, secondT));
                if (overlapStart > overlapEnd + Tolerance)
                {
                    intersection = default;
                    return false;
                }
                intersection = a + firstDirection * Math.Clamp(overlapStart, 0.0, 1.0);
                return true;
            }

            double t = Cross(between, secondDirection) / denominator;
            double u = Cross(between, firstDirection) / denominator;
            if (t < -Tolerance || t > 1.0 + Tolerance || u < -Tolerance || u > 1.0 + Tolerance)
            {
                intersection = default;
                return false;
            }
            intersection = a + firstDirection * Math.Clamp(t, 0.0, 1.0);
            return true;
        }

        public static IReadOnlyList<Vector2> ClipPolygon(IReadOnlyList<Vector2> polygon, Vector2 clipMin, Vector2 clipMax)
        {
            List<Vector2> vertices = NormalizePolygon(polygon, nameof(polygon));
            EnsureFinite(clipMin, nameof(clipMin));
            EnsureFinite(clipMax, nameof(clipMax));
            if (clipMin.X > clipMax.X || clipMin.Y > clipMax.Y)
            {
                throw new ArgumentException("Clip rectangle minimum must not exceed its maximum.");
            }
            vertices = ClipAgainstEdge(vertices, point => point.X >= clipMin.X - Tolerance, (a, b) => IntersectVertical(a, b, clipMin.X));
            vertices = ClipAgainstEdge(vertices, point => point.X <= clipMax.X + Tolerance, (a, b) => IntersectVertical(a, b, clipMax.X));
            vertices = ClipAgainstEdge(vertices, point => point.Y >= clipMin.Y - Tolerance, (a, b) => IntersectHorizontal(a, b, clipMin.Y));
            vertices = ClipAgainstEdge(vertices, point => point.Y <= clipMax.Y + Tolerance, (a, b) => IntersectHorizontal(a, b, clipMax.Y));
            return vertices.AsReadOnly();
        }

        public static IReadOnlyList<Triangle2D> TriangulatePolygon(IReadOnlyList<Vector2> polygon)
        {
            List<Vector2> vertices = NormalizePolygon(polygon, nameof(polygon));
            EnsureSimplePolygon(vertices);
            double signedArea = PolygonSignedArea(vertices);
            if (Math.Abs(signedArea) <= Tolerance)
            {
                throw new ArgumentException("Polygon area must be non-zero.", nameof(polygon));
            }
            double orientation = Math.Sign(signedArea);
            List<int> remaining = new List<int>();
            for (int index = 0; index < vertices.Count; index++)
            {
                remaining.Add(index);
            }
            List<Triangle2D> triangles = new List<Triangle2D>(vertices.Count - 2);
            int attempts = 0;
            while (remaining.Count > 3)
            {
                bool clipped = false;
                for (int position = 0; position < remaining.Count; position++)
                {
                    int previous = remaining[(position + remaining.Count - 1) % remaining.Count];
                    int current = remaining[position];
                    int next = remaining[(position + 1) % remaining.Count];
                    Vector2 a = vertices[previous];
                    Vector2 b = vertices[current];
                    Vector2 c = vertices[next];
                    if (orientation * Cross(b - a, c - b) <= Tolerance)
                    {
                        continue;
                    }
                    bool containsVertex = false;
                    foreach (int candidate in remaining)
                    {
                        if (candidate != previous && candidate != current && candidate != next
                            && PointInTriangle(vertices[candidate], a, b, c, orientation))
                        {
                            containsVertex = true;
                            break;
                        }
                    }
                    if (containsVertex)
                    {
                        continue;
                    }
                    triangles.Add(new Triangle2D(a, b, c));
                    remaining.RemoveAt(position);
                    clipped = true;
                    break;
                }
                attempts++;
                if (!clipped || attempts > vertices.Count * vertices.Count)
                {
                    throw new ArgumentException("Polygon could not be triangulated; it may be degenerate.", nameof(polygon));
                }
            }
            triangles.Add(new Triangle2D(vertices[remaining[0]], vertices[remaining[1]], vertices[remaining[2]]));
            return triangles.AsReadOnly();
        }

        public static BarycentricCoordinates BarycentricCoordinates(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            EnsureFinite(point, nameof(point));
            EnsureFinite(a, nameof(a));
            EnsureFinite(b, nameof(b));
            EnsureFinite(c, nameof(c));
            Vector2 first = b - a;
            Vector2 second = c - a;
            double denominator = Cross(first, second);
            if (Math.Abs(denominator) <= Tolerance)
            {
                throw new ArgumentException("Triangle vertices must not be collinear.");
            }
            Vector2 relative = point - a;
            double secondWeight = Cross(relative, second) / denominator;
            double thirdWeight = Cross(first, relative) / denominator;
            return new BarycentricCoordinates(1.0 - secondWeight - thirdWeight, secondWeight, thirdWeight);
        }

        public static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            BarycentricCoordinates weights = BarycentricCoordinates(point, a, b, c);
            return weights.First >= -Tolerance && weights.Second >= -Tolerance && weights.Third >= -Tolerance
                && weights.First <= 1.0 + Tolerance && weights.Second <= 1.0 + Tolerance && weights.Third <= 1.0 + Tolerance;
        }

        public static Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            EnsureFinite(a, nameof(a));
            EnsureFinite(b, nameof(b));
            EnsureFinite(c, nameof(c));
            return Math_2.NormalizeVector(Math_2.CalculateCrossProduct(b - a, c - a));
        }

        public static double TriangleArea(Vector3 a, Vector3 b, Vector3 c)
        {
            EnsureFinite(a, nameof(a));
            EnsureFinite(b, nameof(b));
            EnsureFinite(c, nameof(c));
            return 0.5 * Magnitude(Math_2.CalculateCrossProduct(b - a, c - a));
        }

        public static BarycentricCoordinates BarycentricCoordinates(Vector3 point, Triangle triangle)
        {
            EnsureFinite(point, nameof(point));
            EnsureFinite(triangle.A, nameof(triangle));
            EnsureFinite(triangle.B, nameof(triangle));
            EnsureFinite(triangle.C, nameof(triangle));
            Vector3 first = triangle.B - triangle.A;
            Vector3 second = triangle.C - triangle.A;
            Vector3 relative = point - triangle.A;
            double d00 = Dot(first, first);
            double d01 = Dot(first, second);
            double d11 = Dot(second, second);
            double d20 = Dot(relative, first);
            double d21 = Dot(relative, second);
            double denominator = d00 * d11 - d01 * d01;
            if (Math.Abs(denominator) <= Tolerance)
            {
                throw new ArgumentException("Triangle vertices must not be collinear.", nameof(triangle));
            }
            double secondWeight = (d11 * d20 - d01 * d21) / denominator;
            double thirdWeight = (d00 * d21 - d01 * d20) / denominator;
            return new BarycentricCoordinates(1.0 - secondWeight - thirdWeight, secondWeight, thirdWeight);
        }

        public static RayHit RayPlaneIntersection(Ray ray, Plane plane)
        {
            Vector3 direction = NormalizeDirection(ray.Direction);
            EnsureFinite(ray.Origin, nameof(ray));
            EnsureFinite(plane.Normal, nameof(plane));
            EnsureFinite(plane.Offset, nameof(plane));
            double normalLength = Magnitude(plane.Normal);
            if (normalLength <= Tolerance)
            {
                throw new ArgumentException("Plane normal must be non-zero.", nameof(plane));
            }
            Vector3 normal = Scale(plane.Normal, 1.0 / normalLength);
            double normalizedOffset = plane.Offset / normalLength;
            double denominator = Dot(normal, direction);
            if (Math.Abs(denominator) <= Tolerance)
            {
                return Miss();
            }
            double distance = (normalizedOffset - Dot(normal, ray.Origin)) / denominator;
            if (distance < 0.0)
            {
                return Miss();
            }
            Vector3 point = ray.Origin + Scale(direction, distance);
            return new RayHit(true, distance, point, denominator < 0.0 ? normal : Scale(normal, -1.0));
        }

        public static RayHit RaySphereIntersection(Ray ray, Vector3 center, double radius)
        {
            Vector3 direction = NormalizeDirection(ray.Direction);
            EnsureFinite(ray.Origin, nameof(ray));
            EnsureFinite(center, nameof(center));
            EnsurePositiveFinite(radius, nameof(radius));
            Vector3 offset = ray.Origin - center;
            double linear = Dot(offset, direction);
            double discriminant = linear * linear - (Dot(offset, offset) - radius * radius);
            if (discriminant < 0.0)
            {
                return Miss();
            }
            double root = Math.Sqrt(discriminant);
            double distance = -linear - root;
            if (distance < 0.0)
            {
                distance = -linear + root;
            }
            if (distance < 0.0)
            {
                return Miss();
            }
            Vector3 point = ray.Origin + Scale(direction, distance);
            Vector3 normal = Math_2.NormalizeVector(point - center);
            return new RayHit(true, distance, point, normal);
        }

        public static RayHit RayTriangleIntersection(Ray ray, Triangle triangle)
        {
            Vector3 direction = NormalizeDirection(ray.Direction);
            EnsureFinite(ray.Origin, nameof(ray));
            EnsureFinite(triangle.A, nameof(triangle));
            EnsureFinite(triangle.B, nameof(triangle));
            EnsureFinite(triangle.C, nameof(triangle));
            Vector3 edge1 = triangle.B - triangle.A;
            Vector3 edge2 = triangle.C - triangle.A;
            Vector3 p = Math_2.CalculateCrossProduct(direction, edge2);
            double determinant = Dot(edge1, p);
            if (Math.Abs(determinant) <= Tolerance)
            {
                return Miss();
            }
            double inverse = 1.0 / determinant;
            Vector3 t = ray.Origin - triangle.A;
            double u = Dot(t, p) * inverse;
            if (u < -Tolerance || u > 1.0 + Tolerance)
            {
                return Miss();
            }
            Vector3 q = Math_2.CalculateCrossProduct(t, edge1);
            double v = Dot(direction, q) * inverse;
            if (v < -Tolerance || u + v > 1.0 + Tolerance)
            {
                return Miss();
            }
            double distance = Dot(edge2, q) * inverse;
            if (distance < 0.0)
            {
                return Miss();
            }
            Vector3 normal = TriangleNormal(triangle.A, triangle.B, triangle.C);
            if (Dot(normal, direction) > 0.0)
            {
                normal = Scale(normal, -1.0);
            }
            return new RayHit(true, distance, ray.Origin + Scale(direction, distance), normal);
        }

        public static RayHit RayBoxIntersection(Ray ray, BoundingBox box)
        {
            Vector3 direction = NormalizeDirection(ray.Direction);
            EnsureFinite(ray.Origin, nameof(ray));
            EnsureFinite(box.Minimum, nameof(box));
            EnsureFinite(box.Maximum, nameof(box));
            if (box.Minimum.X > box.Maximum.X || box.Minimum.Y > box.Maximum.Y || box.Minimum.Z > box.Maximum.Z)
            {
                throw new ArgumentException("Bounding-box minimum must not exceed maximum.", nameof(box));
            }
            double near = double.NegativeInfinity;
            double far = double.PositiveInfinity;
            Vector3 nearNormal = default;
            Vector3 farNormal = default;
            for (int axis = 0; axis < 3; axis++)
            {
                double origin = Component(ray.Origin, axis);
                double delta = Component(direction, axis);
                double minimum = Component(box.Minimum, axis);
                double maximum = Component(box.Maximum, axis);
                if (Math.Abs(delta) <= Tolerance)
                {
                    if (origin < minimum || origin > maximum)
                    {
                        return Miss();
                    }
                    continue;
                }
                double first = (minimum - origin) / delta;
                double second = (maximum - origin) / delta;
                Vector3 firstNormal = AxisVector(axis, -1.0);
                Vector3 secondNormal = AxisVector(axis, 1.0);
                if (first > second)
                {
                    (first, second) = (second, first);
                    (firstNormal, secondNormal) = (secondNormal, firstNormal);
                }
                if (first > near)
                {
                    near = first;
                    nearNormal = firstNormal;
                }
                if (second < far)
                {
                    far = second;
                    farNormal = secondNormal;
                }
                if (near > far)
                {
                    return Miss();
                }
            }
            double distance = near >= 0.0 ? near : far;
            Vector3 normal = near >= 0.0 ? nearNormal : farNormal;
            if (distance < 0.0 || !double.IsFinite(distance))
            {
                return Miss();
            }
            return new RayHit(true, distance, ray.Origin + Scale(direction, distance), normal);
        }

        public static BoundingBox BoundingBoxFromPoints(IReadOnlyList<Vector3> points)
        {
            ValidatePoints(points, 1, nameof(points));
            double minX = points[0].X;
            double minY = points[0].Y;
            double minZ = points[0].Z;
            double maxX = minX;
            double maxY = minY;
            double maxZ = minZ;
            foreach (Vector3 point in points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                minZ = Math.Min(minZ, point.Z);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
                maxZ = Math.Max(maxZ, point.Z);
            }
            return new BoundingBox(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
        }

        public static Vector3 BezierPoint(IReadOnlyList<Vector3> controlPoints, double t)
        {
            ValidateCurve(controlPoints, t);
            Vector3[] work = new Vector3[controlPoints.Count];
            for (int index = 0; index < work.Length; index++)
            {
                work[index] = controlPoints[index];
            }
            for (int level = work.Length - 1; level > 0; level--)
            {
                for (int index = 0; index < level; index++)
                {
                    work[index] = Add(Scale(work[index], 1.0 - t), Scale(work[index + 1], t));
                }
            }
            return work[0];
        }

        public static Vector3 BezierDerivative(IReadOnlyList<Vector3> controlPoints, double t)
        {
            ValidateCurve(controlPoints, t);
            if (controlPoints.Count == 1)
            {
                return new Vector3(0.0, 0.0, 0.0);
            }
            int degree = controlPoints.Count - 1;
            Vector3[] derivativeControls = new Vector3[degree];
            for (int index = 0; index < degree; index++)
            {
                derivativeControls[index] = Scale(controlPoints[index + 1] - controlPoints[index], degree);
            }
            return BezierPoint(derivativeControls, t);
        }

        public static Vector3 CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, double t)
        {
            EnsureFinite(p0, nameof(p0));
            EnsureFinite(p1, nameof(p1));
            EnsureFinite(p2, nameof(p2));
            EnsureFinite(p3, nameof(p3));
            EnsureUnitParameter(t);
            double t2 = t * t;
            double t3 = t2 * t;
            return Scale(Add(Add(Scale(p1, 2.0), Scale(p2 - p0, t)),
                    Add(Scale(Add(Add(Scale(p0, 2.0), Scale(p1, -5.0)), Add(Scale(p2, 4.0), Scale(p3, -1.0))), t2),
                        Scale(Add(Add(Scale(p0, -1.0), Scale(p1, 3.0)), Add(Scale(p2, -3.0), p3)), t3))),
                0.5);
        }

        public static Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, double t)
        {
            EnsureFinite(p0, nameof(p0));
            EnsureFinite(p1, nameof(p1));
            EnsureFinite(p2, nameof(p2));
            EnsureFinite(p3, nameof(p3));
            EnsureUnitParameter(t);
            double t2 = t * t;
            Vector3 secondCoefficient = Add(Add(Scale(p0, 2.0), Scale(p1, -5.0)), Add(Scale(p2, 4.0), Scale(p3, -1.0)));
            Vector3 thirdCoefficient = Add(Add(Scale(p0, -1.0), Scale(p1, 3.0)), Add(Scale(p2, -3.0), p3));
            return Scale(Add(Add(p2 - p0, Scale(secondCoefficient, 2.0 * t)), Scale(thirdCoefficient, 3.0 * t2)), 0.5);
        }

        public static double[,] TranslationMatrix(double x, double y, double z)
        {
            EnsureFinite(x, nameof(x));
            EnsureFinite(y, nameof(y));
            EnsureFinite(z, nameof(z));
            double[,] result = IdentityMatrix();
            result[0, 3] = x;
            result[1, 3] = y;
            result[2, 3] = z;
            return result;
        }

        public static double[,] RotationXMatrix(double radians)
        {
            EnsureFinite(radians, nameof(radians));
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            double[,] result = IdentityMatrix();
            result[1, 1] = cosine;
            result[1, 2] = -sine;
            result[2, 1] = sine;
            result[2, 2] = cosine;
            return result;
        }

        /// <summary>
        /// Transforms 3D direction vector by transformation matrix.
        /// </summary>
        public static double[,] RotationYMatrix(double radians)
        {
            EnsureFinite(radians, nameof(radians));
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            double[,] result = IdentityMatrix();
            result[0, 0] = cosine;
            result[0, 2] = sine;
            result[2, 0] = -sine;
            result[2, 2] = cosine;
            return result;
        }

        public static double[,] RotationZMatrix(double radians)
        {
            EnsureFinite(radians, nameof(radians));
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            double[,] result = IdentityMatrix();
            result[0, 0] = cosine;
            result[0, 1] = -sine;
            result[1, 0] = sine;
            result[1, 1] = cosine;
            return result;
        }

        public static double[,] ScaleMatrix(double x, double y, double z)
        {
            EnsureFinite(x, nameof(x));
            EnsureFinite(y, nameof(y));
            EnsureFinite(z, nameof(z));
            double[,] result = IdentityMatrix();
            result[0, 0] = x;
            result[1, 1] = y;
            result[2, 2] = z;
            return result;
        }

        public static double[,] MultiplyMatrices(double[,] left, double[,] right)
        {
            ValidateTransformMatrix(left, nameof(left));
            ValidateTransformMatrix(right, nameof(right));
            return Algebra.MatrixMultiply(left, right);
        }

        public static Vector3 TransformPoint(double[,] matrix, Vector3 point)
        {
            ValidateTransformMatrix(matrix, nameof(matrix));
            EnsureFinite(point, nameof(point));
            double x = matrix[0, 0] * point.X + matrix[0, 1] * point.Y + matrix[0, 2] * point.Z + matrix[0, 3];
            double y = matrix[1, 0] * point.X + matrix[1, 1] * point.Y + matrix[1, 2] * point.Z + matrix[1, 3];
            double z = matrix[2, 0] * point.X + matrix[2, 1] * point.Y + matrix[2, 2] * point.Z + matrix[2, 3];
            double w = matrix[3, 0] * point.X + matrix[3, 1] * point.Y + matrix[3, 2] * point.Z + matrix[3, 3];
            if (Math.Abs(w) <= Tolerance)
            {
                throw new InvalidOperationException("Point transform produced a zero homogeneous coordinate.");
            }
            return new Vector3(x / w, y / w, z / w);
        }

        public static Vector3 TransformDirection(double[,] matrix, Vector3 direction)
        {
            ValidateTransformMatrix(matrix, nameof(matrix));
            EnsureFinite(direction, nameof(direction));
            return new Vector3(
                matrix[0, 0] * direction.X + matrix[0, 1] * direction.Y + matrix[0, 2] * direction.Z,
                matrix[1, 0] * direction.X + matrix[1, 1] * direction.Y + matrix[1, 2] * direction.Z,
                matrix[2, 0] * direction.X + matrix[2, 1] * direction.Y + matrix[2, 2] * direction.Z);
        }

        public static double[,] PerspectiveProjection(double fieldOfViewRadians, double aspectRatio, double nearPlane, double farPlane)
        {
            EnsureFinite(fieldOfViewRadians, nameof(fieldOfViewRadians));
            EnsurePositiveFinite(aspectRatio, nameof(aspectRatio));
            EnsurePositiveFinite(nearPlane, nameof(nearPlane));
            EnsurePositiveFinite(farPlane, nameof(farPlane));
            if (fieldOfViewRadians <= 0.0 || fieldOfViewRadians >= Math.PI)
            {
                throw new ArgumentOutOfRangeException(nameof(fieldOfViewRadians));
            }
            if (farPlane <= nearPlane)
            {
                throw new ArgumentOutOfRangeException(nameof(farPlane), "Far plane must exceed near plane.");
            }
            double scale = 1.0 / Math.Tan(fieldOfViewRadians * 0.5);
            return new[,]
            {
                { scale / aspectRatio, 0.0, 0.0, 0.0 },
                { 0.0, scale, 0.0, 0.0 },
                { 0.0, 0.0, (farPlane + nearPlane) / (nearPlane - farPlane), (2.0 * farPlane * nearPlane) / (nearPlane - farPlane) },
                { 0.0, 0.0, -1.0, 0.0 }
            };
        }

        public static double[,] LookAt(Vector3 eye, Vector3 target, Vector3 up)
        {
            EnsureFinite(eye, nameof(eye));
            EnsureFinite(target, nameof(target));
            EnsureFinite(up, nameof(up));
            Vector3 forward = Normalize(target - eye, nameof(target));
            Vector3 side = Normalize(Math_2.CalculateCrossProduct(forward, up), nameof(up));
            Vector3 correctedUp = Math_2.CalculateCrossProduct(side, forward);
            return new[,]
            {
                { side.X, side.Y, side.Z, -Dot(side, eye) },
                { correctedUp.X, correctedUp.Y, correctedUp.Z, -Dot(correctedUp, eye) },
                { -forward.X, -forward.Y, -forward.Z, Dot(forward, eye) },
                { 0.0, 0.0, 0.0, 1.0 }
            };
        }

        private static List<Vector2> NormalizePolygon(IReadOnlyList<Vector2> polygon, string parameterName)
        {
            ValidatePoints(polygon, 3, parameterName);
            List<Vector2> result = new List<Vector2>(polygon);
            if (result.Count > 3 && SamePoint(result[0], result[^1]))
            {
                result.RemoveAt(result.Count - 1);
            }
            if (result.Count < 3)
            {
                throw new ArgumentException("Polygon must contain at least three distinct vertices.", parameterName);
            }
            return result;
        }

        private static List<Vector2> ClipAgainstEdge(List<Vector2> input, Func<Vector2, bool> inside, Func<Vector2, Vector2, Vector2> intersection)
        {
            List<Vector2> output = new List<Vector2>();
            if (input.Count == 0)
            {
                return output;
            }
            Vector2 previous = input[^1];
            bool previousInside = inside(previous);
            foreach (Vector2 current in input)
            {
                bool currentInside = inside(current);
                if (currentInside)
                {
                    if (!previousInside)
                    {
                        output.Add(intersection(previous, current));
                    }
                    output.Add(current);
                }
                else if (previousInside)
                {
                    output.Add(intersection(previous, current));
                }
                previous = current;
                previousInside = currentInside;
            }
            return RemoveConsecutiveDuplicates(output);
        }

        private static List<Vector2> RemoveConsecutiveDuplicates(List<Vector2> points)
        {
            List<Vector2> result = new List<Vector2>();
            foreach (Vector2 point in points)
            {
                if (result.Count == 0 || !SamePoint(result[^1], point))
                {
                    result.Add(point);
                }
            }
            if (result.Count > 1 && SamePoint(result[0], result[^1]))
            {
                result.RemoveAt(result.Count - 1);
            }
            return result;
        }

        private static Vector2 IntersectVertical(Vector2 first, Vector2 second, double x)
        {
            double difference = second.X - first.X;
            if (Math.Abs(difference) <= Tolerance)
            {
                return new Vector2(x, first.Y);
            }
            double t = (x - first.X) / difference;
            return new Vector2(x, first.Y + t * (second.Y - first.Y));
        }

        private static Vector2 IntersectHorizontal(Vector2 first, Vector2 second, double y)
        {
            double difference = second.Y - first.Y;
            if (Math.Abs(difference) <= Tolerance)
            {
                return new Vector2(first.X, y);
            }
            double t = (y - first.Y) / difference;
            return new Vector2(first.X + t * (second.X - first.X), y);
        }

        private static void EnsureSimplePolygon(List<Vector2> polygon)
        {
            int count = polygon.Count;
            for (int first = 0; first < count; first++)
            {
                int firstNext = (first + 1) % count;
                for (int second = first + 1; second < count; second++)
                {
                    int secondNext = (second + 1) % count;
                    if (first == second || firstNext == second || secondNext == first)
                    {
                        continue;
                    }
                    if (SegmentIntersection(polygon[first], polygon[firstNext], polygon[second], polygon[secondNext], out _))
                    {
                        throw new ArgumentException("Polygon must be simple and non-self-intersecting.", nameof(polygon));
                    }
                }
            }
        }

        private static double PolygonSignedArea(List<Vector2> polygon)
        {
            double area = 0.0;
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 current = polygon[index];
                Vector2 next = polygon[(index + 1) % polygon.Count];
                area += current.X * next.Y - next.X * current.Y;
            }
            return 0.5 * area;
        }

        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c, double orientation)
        {
            double first = orientation * Cross(b - a, point - a);
            double second = orientation * Cross(c - b, point - b);
            double third = orientation * Cross(a - c, point - c);
            return first >= -Tolerance && second >= -Tolerance && third >= -Tolerance;
        }

        private static bool PointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            Vector2 relative = point - a;
            if (Math.Abs(Cross(edge, relative)) > Tolerance * (1.0 + Magnitude(edge)))
            {
                return false;
            }
            double projection = Dot(relative, edge);
            return projection >= -Tolerance && projection <= Dot(edge, edge) + Tolerance;
        }

        private static bool SamePoint(Vector2 first, Vector2 second)
        {
            return Math.Abs(first.X - second.X) <= Tolerance && Math.Abs(first.Y - second.Y) <= Tolerance;
        }

        private static int ComparePoints(Vector2 first, Vector2 second)
        {
            int xComparison = first.X.CompareTo(second.X);
            return xComparison != 0 ? xComparison : first.Y.CompareTo(second.Y);
        }

        private static double Cross(Vector2 first, Vector2 second)
        {
            return first.X * second.Y - first.Y * second.X;
        }

        private static double Cross(Vector3 first, Vector3 second)
        {
            return first.X * second.Y - first.Y * second.X;
        }

        private static double Dot(Vector2 first, Vector2 second)
        {
            return first.X * second.X + first.Y * second.Y;
        }

        private static double Dot(Vector3 first, Vector3 second)
        {
            return first.X * second.X + first.Y * second.Y + first.Z * second.Z;
        }

        private static double Magnitude(Vector2 value)
        {
            return Math.Sqrt(Dot(value, value));
        }

        private static double Magnitude(Vector3 value)
        {
            return Math.Sqrt(Dot(value, value));
        }

        private static Vector3 NormalizeDirection(Vector3 direction)
        {
            return Normalize(direction, nameof(direction));
        }

        private static Vector3 Normalize(Vector3 value, string parameterName)
        {
            double magnitude = Magnitude(value);
            if (!double.IsFinite(magnitude) || magnitude <= Tolerance)
            {
                throw new ArgumentException("Vector must be finite and non-zero.", parameterName);
            }
            return Scale(value, 1.0 / magnitude);
        }

        private static Vector3 Add(Vector3 first, Vector3 second)
        {
            return new Vector3(first.X + second.X, first.Y + second.Y, first.Z + second.Z);
        }

        private static Vector3 Scale(Vector3 value, double factor)
        {
            return new Vector3(value.X * factor, value.Y * factor, value.Z * factor);
        }

        private static double Component(Vector3 value, int axis)
        {
            return axis switch
            {
                0 => value.X,
                1 => value.Y,
                2 => value.Z,
                _ => throw new ArgumentOutOfRangeException(nameof(axis))
            };
        }

        private static Vector3 AxisVector(int axis, double sign)
        {
            return axis switch
            {
                0 => new Vector3(sign, 0.0, 0.0),
                1 => new Vector3(0.0, sign, 0.0),
                2 => new Vector3(0.0, 0.0, sign),
                _ => throw new ArgumentOutOfRangeException(nameof(axis))
            };
        }

        private static RayHit Miss()
        {
            return new RayHit(false, double.PositiveInfinity, default, default);
        }

        private static List<Vector2> ValidatePoints(IReadOnlyList<Vector2> points, int minimumCount, string parameterName)
        {
            if (points == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (points.Count < minimumCount)
            {
                throw new ArgumentException($"At least {minimumCount} points are required.", parameterName);
            }
            List<Vector2> copy = new List<Vector2>(points.Count);
            foreach (Vector2 point in points)
            {
                EnsureFinite(point, parameterName);
                copy.Add(point);
            }
            return copy;
        }

        private static void ValidatePoints(IReadOnlyList<Vector3> points, int minimumCount, string parameterName)
        {
            if (points == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (points.Count < minimumCount)
            {
                throw new ArgumentException($"At least {minimumCount} points are required.", parameterName);
            }
            foreach (Vector3 point in points)
            {
                EnsureFinite(point, parameterName);
            }
        }

        private static void ValidateCurve(IReadOnlyList<Vector3> controlPoints, double t)
        {
            ValidatePoints(controlPoints, 1, nameof(controlPoints));
            EnsureUnitParameter(t);
        }

        private static void EnsureUnitParameter(double value)
        {
            if (!double.IsFinite(value) || value < 0.0 || value > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Curve parameter must be in [0, 1].");
            }
        }

        private static void ValidateTransformMatrix(double[,] matrix, string parameterName)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (matrix.GetLength(0) != 4 || matrix.GetLength(1) != 4)
            {
                throw new ArgumentException("Transform matrices must be 4x4.", parameterName);
            }
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    EnsureFinite(matrix[row, column], parameterName);
                }
            }
        }

        private static double[,] IdentityMatrix()
        {
            return new[,]
            {
                { 1.0, 0.0, 0.0, 0.0 },
                { 0.0, 1.0, 0.0, 0.0 },
                { 0.0, 0.0, 1.0, 0.0 },
                { 0.0, 0.0, 0.0, 1.0 }
            };
        }

        private static void EnsureFinite(Vector2 value, string parameterName)
        {
            EnsureFinite(value.X, parameterName);
            EnsureFinite(value.Y, parameterName);
        }

        private static void EnsureFinite(Vector3 value, string parameterName)
        {
            EnsureFinite(value.X, parameterName);
            EnsureFinite(value.Y, parameterName);
            EnsureFinite(value.Z, parameterName);
        }

        private static void EnsureFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            }
        }

        private static void EnsurePositiveFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be positive and finite.");
            }
        }
    }
}
