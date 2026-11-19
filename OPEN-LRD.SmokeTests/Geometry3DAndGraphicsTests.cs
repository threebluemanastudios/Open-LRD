using System;
using System.Collections.Generic;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class Geometry3DAndGraphicsTests
    {
        public static void RunAll()
        {
            Run("2D hull, polygon, and segment operations", TestPlanarGeometry);
            Run("Polygon triangulation and barycentric coordinates", TestTriangulation);
            Run("3D triangle and bounding-box geometry", TestSolidGeometry);
            Run("Ray intersections", TestRayIntersections);
            Run("Bezier and Catmull-Rom curves", TestCurves);
            Run("3D transform matrices", TestTransforms);
            Run("Geometry validation", TestValidation);
        }

        private static void TestPlanarGeometry()
        {
            IReadOnlyList<Vector2> hull = Geometry3DAndGraphics.ConvexHull(new[]
            {
                new Vector2(0.0, 0.0),
                new Vector2(1.0, 0.0),
                new Vector2(1.0, 1.0),
                new Vector2(0.0, 1.0),
                new Vector2(0.5, 0.5),
                new Vector2(0.0, 0.0)
            });
            Assert(hull.Count == 4, "convex hull removes interior and duplicate points");
            AssertEqual(hull[0].X, 0.0, "hull starts at minimum x/y point");
            Assert(Geometry3DAndGraphics.IsPointInPolygon(new Vector2(0.5, 0.5), hull), "point inside polygon");
            Assert(!Geometry3DAndGraphics.IsPointInPolygon(new Vector2(2.0, 0.5), hull), "point outside polygon");
            Assert(Geometry3DAndGraphics.IsPointInPolygon(new Vector2(1.0, 0.5), hull), "boundary point belongs to polygon");

            Assert(
                Geometry3DAndGraphics.SegmentIntersection(
                    new Vector2(0.0, 0.0), new Vector2(2.0, 2.0),
                    new Vector2(0.0, 2.0), new Vector2(2.0, 0.0),
                    out Vector2 crossing),
                "crossing segments should intersect");
            AssertEqual(crossing.X, 1.0, "segment intersection x");
            AssertEqual(crossing.Y, 1.0, "segment intersection y");

            IReadOnlyList<Vector2> clipped = Geometry3DAndGraphics.ClipPolygon(
                new[] { new Vector2(-1.0, -1.0), new Vector2(2.0, -1.0), new Vector2(2.0, 2.0), new Vector2(-1.0, 2.0) },
                new Vector2(0.0, 0.0),
                new Vector2(1.0, 1.0));
            Assert(clipped.Count == 4, "clipped square vertex count");
            foreach (Vector2 point in clipped)
            {
                Assert(point.X >= -1e-10 && point.X <= 1.0 + 1e-10, "clipped x coordinate");
                Assert(point.Y >= -1e-10 && point.Y <= 1.0 + 1e-10, "clipped y coordinate");
            }
        }

        private static void TestTriangulation()
        {
            Vector2 a = new Vector2(0.0, 0.0);
            Vector2 b = new Vector2(2.0, 0.0);
            Vector2 c = new Vector2(2.0, 2.0);
            Vector2 d = new Vector2(0.0, 2.0);
            IReadOnlyList<Triangle2D> triangles = Geometry3DAndGraphics.TriangulatePolygon(new[] { a, b, c, d });
            Assert(triangles.Count == 2, "quadrilateral should produce n-2 triangles");
            double area = 0.0;
            foreach (Triangle2D triangle in triangles)
            {
                area += Math.Abs((triangle.B.X - triangle.A.X) * (triangle.C.Y - triangle.A.Y)
                    - (triangle.B.Y - triangle.A.Y) * (triangle.C.X - triangle.A.X)) * 0.5;
            }
            AssertEqual(area, 4.0, "triangulated area");

            BarycentricCoordinates weights = Geometry3DAndGraphics.BarycentricCoordinates(new Vector2(0.5, 0.5), a, b, d);
            AssertEqual(weights.First + weights.Second + weights.Third, 1.0, "2D barycentric weights sum");
            Assert(Geometry3DAndGraphics.IsPointInTriangle(new Vector2(0.5, 0.5), a, b, d), "point inside triangle");
            Assert(!Geometry3DAndGraphics.IsPointInTriangle(new Vector2(1.8, 1.8), a, b, d), "point outside triangle");
        }

        private static void TestSolidGeometry()
        {
            Vector3 a = new Vector3(0.0, 0.0, 0.0);
            Vector3 b = new Vector3(1.0, 0.0, 0.0);
            Vector3 c = new Vector3(0.0, 1.0, 0.0);
            Vector3 normal = Geometry3DAndGraphics.TriangleNormal(a, b, c);
            AssertEqual(normal.X, 0.0, "triangle normal x");
            AssertEqual(normal.Y, 0.0, "triangle normal y");
            AssertEqual(normal.Z, 1.0, "triangle normal z");
            AssertEqual(Geometry3DAndGraphics.TriangleArea(a, b, c), 0.5, "triangle area");
            BarycentricCoordinates barycentric = Geometry3DAndGraphics.BarycentricCoordinates(new Vector3(0.25, 0.25, 0.0), new Triangle(a, b, c));
            AssertEqual(barycentric.First + barycentric.Second + barycentric.Third, 1.0, "3D barycentric weights sum");
            AssertEqual(barycentric.First, 0.5, "3D barycentric first weight");

            BoundingBox bounds = Geometry3DAndGraphics.BoundingBoxFromPoints(new[]
            {
                new Vector3(-1.0, 2.0, 0.0),
                new Vector3(3.0, -2.0, 5.0)
            });
            AssertEqual(bounds.Minimum.X, -1.0, "bounding-box minimum x");
            AssertEqual(bounds.Minimum.Y, -2.0, "bounding-box minimum y");
            AssertEqual(bounds.Maximum.Z, 5.0, "bounding-box maximum z");
        }

        private static void TestRayIntersections()
        {
            RayHit planeHit = Geometry3DAndGraphics.RayPlaneIntersection(
                new Ray(new Vector3(0.0, 0.0, 1.0), new Vector3(0.0, 0.0, -2.0)),
                new Plane(new Vector3(0.0, 0.0, 1.0), 0.0));
            Assert(planeHit.Hit, "ray should hit plane");
            AssertEqual(planeHit.Distance, 1.0, "ray-plane distance");
            AssertEqual(planeHit.Point.Z, 0.0, "ray-plane point");

            RayHit sphereHit = Geometry3DAndGraphics.RaySphereIntersection(
                new Ray(new Vector3(0.0, 0.0, 3.0), new Vector3(0.0, 0.0, -1.0)),
                new Vector3(0.0, 0.0, 0.0),
                1.0);
            Assert(sphereHit.Hit, "ray should hit sphere");
            AssertEqual(sphereHit.Distance, 2.0, "ray-sphere distance");

            Triangle triangle = new Triangle(new Vector3(0.0, 0.0, 0.0), new Vector3(1.0, 0.0, 0.0), new Vector3(0.0, 1.0, 0.0));
            RayHit triangleHit = Geometry3DAndGraphics.RayTriangleIntersection(
                new Ray(new Vector3(0.25, 0.25, 1.0), new Vector3(0.0, 0.0, -1.0)),
                triangle);
            Assert(triangleHit.Hit, "ray should hit triangle");
            AssertEqual(triangleHit.Distance, 1.0, "ray-triangle distance");
            Assert(!Geometry3DAndGraphics.RayTriangleIntersection(
                new Ray(new Vector3(2.0, 2.0, 1.0), new Vector3(0.0, 0.0, -1.0)),
                triangle).Hit,
                "ray outside triangle should miss");

            RayHit boxHit = Geometry3DAndGraphics.RayBoxIntersection(
                new Ray(new Vector3(-2.0, 0.0, 0.0), new Vector3(1.0, 0.0, 0.0)),
                new BoundingBox(new Vector3(-1.0, -1.0, -1.0), new Vector3(1.0, 1.0, 1.0)));
            Assert(boxHit.Hit, "ray should hit box");
            AssertEqual(boxHit.Distance, 1.0, "ray-box distance");
        }

        private static void TestCurves()
        {
            Vector3[] controls = { new Vector3(0.0, 0.0, 0.0), new Vector3(1.0, 0.0, 0.0) };
            AssertEqual(Geometry3DAndGraphics.BezierPoint(controls, 0.25).X, 0.25, "linear Bezier point");
            AssertEqual(Geometry3DAndGraphics.BezierDerivative(controls, 0.5).X, 1.0, "linear Bezier derivative");
            Vector3 p0 = new Vector3(0.0, 0.0, 0.0);
            Vector3 p1 = new Vector3(1.0, 0.0, 0.0);
            Vector3 p2 = new Vector3(2.0, 0.0, 0.0);
            Vector3 p3 = new Vector3(3.0, 0.0, 0.0);
            AssertEqual(Geometry3DAndGraphics.CatmullRomPoint(p0, p1, p2, p3, 0.5).X, 1.5, "Catmull-Rom linear interpolation");
            AssertEqual(Geometry3DAndGraphics.CatmullRomTangent(p0, p1, p2, p3, 0.5).X, 1.0, "Catmull-Rom tangent");
        }

        private static void TestTransforms()
        {
            Vector3 point = Geometry3DAndGraphics.TransformPoint(Geometry3DAndGraphics.TranslationMatrix(2.0, 3.0, 4.0), new Vector3(1.0, 1.0, 1.0));
            AssertEqual(point.X, 3.0, "translated point x");
            AssertEqual(point.Y, 4.0, "translated point y");
            AssertEqual(point.Z, 5.0, "translated point z");

            Vector3 direction = Geometry3DAndGraphics.TransformDirection(Geometry3DAndGraphics.TranslationMatrix(2.0, 3.0, 4.0), new Vector3(1.0, 2.0, 3.0));
            AssertEqual(direction.X, 1.0, "translation does not affect direction");
            AssertEqual(Geometry3DAndGraphics.TransformPoint(Geometry3DAndGraphics.RotationZMatrix(Math.PI / 2.0), new Vector3(1.0, 0.0, 0.0)).Y, 1.0, "Z rotation");

            double[,] composition = Geometry3DAndGraphics.MultiplyMatrices(
                Geometry3DAndGraphics.TranslationMatrix(2.0, 0.0, 0.0),
                Geometry3DAndGraphics.ScaleMatrix(2.0, 2.0, 2.0));
            Vector3 transformed = Geometry3DAndGraphics.TransformPoint(composition, new Vector3(1.0, 0.0, 0.0));
            AssertEqual(transformed.X, 4.0, "transform composition");
            Vector3 roundTrip = Geometry3DAndGraphics.TransformPoint(
                Algebra.MatrixInverse(composition),
                transformed);
            AssertEqual(roundTrip.X, 1.0, "inverse transform round trip");

            double[,] projection = Geometry3DAndGraphics.PerspectiveProjection(Math.PI / 2.0, 1.0, 1.0, 10.0);
            double nearClipZ = projection[2, 2] * -1.0 + projection[2, 3];
            double nearClipW = projection[3, 2] * -1.0;
            AssertEqual(nearClipZ / nearClipW, -1.0, "perspective near-plane mapping");

            double[,] lookAt = Geometry3DAndGraphics.LookAt(new Vector3(0.0, 0.0, 1.0), new Vector3(0.0, 0.0, 0.0), new Vector3(0.0, 1.0, 0.0));
            AssertEqual(Geometry3DAndGraphics.TransformPoint(lookAt, new Vector3(0.0, 0.0, 0.0)).Z, -1.0, "look-at target depth");
        }

        private static void TestValidation()
        {
            AssertThrows<DivideByZeroException>(
                () => Geometry3DAndGraphics.TriangleNormal(new Vector3(0.0, 0.0, 0.0), new Vector3(1.0, 0.0, 0.0), new Vector3(2.0, 0.0, 0.0)),
                "degenerate triangle normal");
            AssertThrows<ArgumentException>(
                () => Geometry3DAndGraphics.TriangulatePolygon(new[]
                {
                    new Vector2(0.0, 0.0), new Vector2(2.0, 2.0), new Vector2(0.0, 2.0), new Vector2(2.0, 0.0)
                }),
                "self-intersecting polygon");
            AssertThrows<ArgumentException>(
                () => Geometry3DAndGraphics.RaySphereIntersection(
                    new Ray(new Vector3(0.0, 0.0, 0.0), new Vector3(0.0, 0.0, 0.0)),
                    new Vector3(0.0, 0.0, 0.0),
                    1.0),
                "zero ray direction");
            AssertThrows<ArgumentOutOfRangeException>(
                () => Geometry3DAndGraphics.PerspectiveProjection(Math.PI, 1.0, 1.0, 10.0),
                "invalid field of view");
        }
    }
}
