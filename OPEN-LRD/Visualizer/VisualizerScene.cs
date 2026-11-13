using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using OPEN_LRD.Visualization;

namespace OPEN_LRD.Visualizer
{
    internal sealed class VisualizerScene
    {
        private static readonly Vector3D InitialLookDirection =
            Normalize(new Vector3D(-1.0, -0.35, -1.0));

        private readonly Viewport3D viewport;
        private readonly Model3DGroup sceneGroup = new Model3DGroup();
        private readonly PerspectiveCamera camera = new PerspectiveCamera
        {
            FieldOfView = 55.0,
            UpDirection = new Vector3D(0.0, 1.0, 0.0),
            NearPlaneDistance = 0.05,
            FarPlaneDistance = 5000.0
        };
        private readonly List<GeometryModel3D> geometryModels = new List<GeometryModel3D>();

        private Vector3 center;
        private float radius;

        public VisualizerScene(Viewport3D viewport)
        {
            this.viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));

            camera.Position = new Point3D(0.0, 0.0, -10.0);
            camera.LookDirection = InitialLookDirection;
            viewport.Camera = camera;

            sceneGroup.Children.Add(new AmbientLight(Color.FromRgb(90, 90, 90)));
            sceneGroup.Children.Add(new DirectionalLight(
                Color.FromRgb(230, 230, 230),
                new Vector3D(1.0, -1.0, -1.0)));
            sceneGroup.Children.Add(new DirectionalLight(
                Color.FromRgb(140, 155, 180),
                new Vector3D(-1.0, 0.5, 1.0)));

            viewport.Children.Add(new ModelVisual3D { Content = sceneGroup });
        }

        public bool HasGeometry => geometryModels.Count > 0;

        public void Apply(SceneGeometry geometry, GraphColorMap colors)
        {
            ArgumentNullException.ThrowIfNull(geometry);
            ArgumentNullException.ThrowIfNull(colors);

            ClearGeometry();

            foreach (NodeMeshBatch batch in geometry.NodeBatches)
            {
                Material material = CreateNodeMaterial(colors.GetNodeColor(batch.NodeTypeId));
                AddModel(batch.Mesh, material, backMaterial: null);
            }

            foreach (LinkMeshBatch batch in geometry.LinkBatches)
            {
                Material material = CreateLinkMaterial(
                    colors.GetLinkColor(batch.ForwardTypeId, batch.ClassId));
                AddModel(batch.Mesh, material, material);
            }

            center = geometry.Center;
            radius = geometry.Radius;
            FrameAll();
        }

        public void Clear()
        {
            ClearGeometry();
            center = default;
            radius = 0f;
        }

        public void FrameAll()
        {
            if (!HasGeometry)
                return;

            Frame(center, Math.Max(radius, 0.5f));
        }

        public void ResetView()
        {
            camera.UpDirection = new Vector3D(0.0, 1.0, 0.0);
            camera.LookDirection = InitialLookDirection;
            FrameAll();
        }

        private void Frame(System.Numerics.Vector3 target, float targetRadius)
        {
            double width = viewport.ActualWidth > 1.0 ? viewport.ActualWidth : 1600.0;
            double height = viewport.ActualHeight > 1.0 ? viewport.ActualHeight : 900.0;

            CameraView current = new CameraView(
                ToVector3(camera.Position),
                ToVector3(camera.LookDirection),
                ToVector3(camera.UpDirection),
                camera.FieldOfView,
                width,
                height,
                camera.NearPlaneDistance,
                camera.FarPlaneDistance);

            CameraView framed = ProjectionMath.FrameBounds(current, target, targetRadius);

            camera.Position = ToPoint3D(framed.Position);
            camera.LookDirection = ToVector3D(framed.LookDirection);
            camera.UpDirection = ToVector3D(framed.Up);
            camera.NearPlaneDistance = framed.NearPlaneDistance;
            camera.FarPlaneDistance = framed.FarPlaneDistance;
        }

        private void AddModel(MeshData data, Material material, Material? backMaterial)
        {
            if (data.IsEmpty)
                return;

            GeometryModel3D model = new GeometryModel3D(CreateMesh(data), material)
            {
                BackMaterial = backMaterial
            };
            model.Freeze();
            geometryModels.Add(model);
            sceneGroup.Children.Add(model);
        }

        private void ClearGeometry()
        {
            foreach (GeometryModel3D model in geometryModels)
                sceneGroup.Children.Remove(model);

            geometryModels.Clear();
        }

        private static MeshGeometry3D CreateMesh(MeshData data)
        {
            Point3DCollection positions = new Point3DCollection(data.VertexCount);
            Vector3DCollection normals = new Vector3DCollection(data.VertexCount);
            Int32Collection indices = new Int32Collection(data.IndexCount);

            for (int index = 0; index < data.VertexCount; index++)
            {
                int offset = index * 3;
                positions.Add(new Point3D(
                    data.Positions[offset],
                    data.Positions[offset + 1],
                    data.Positions[offset + 2]));
                normals.Add(new Vector3D(
                    data.Normals[offset],
                    data.Normals[offset + 1],
                    data.Normals[offset + 2]));
            }

            foreach (int index in data.Indices)
                indices.Add(index);

            MeshGeometry3D mesh = new MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                TriangleIndices = indices
            };
            mesh.Freeze();
            return mesh;
        }

        private static Material CreateNodeMaterial(RgbColor color)
        {
            MaterialGroup group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(CreateBrush(color)));
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 40.0));
            group.Freeze();
            return group;
        }

        private static Material CreateLinkMaterial(RgbColor color)
        {
            RgbColor glow = new RgbColor(
                (byte)(color.R * 0.45),
                (byte)(color.G * 0.45),
                (byte)(color.B * 0.45));

            MaterialGroup group = new MaterialGroup();
            group.Children.Add(new EmissiveMaterial(CreateBrush(glow)));
            group.Children.Add(new DiffuseMaterial(CreateBrush(color)));
            group.Freeze();
            return group;
        }

        private static SolidColorBrush CreateBrush(RgbColor color)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }

        private static Vector3D Normalize(Vector3D value)
        {
            value.Normalize();
            return value;
        }

        private static System.Numerics.Vector3 ToVector3(Vector3D value)
        {
            return new System.Numerics.Vector3(
                (float)value.X,
                (float)value.Y,
                (float)value.Z);
        }

        private static System.Numerics.Vector3 ToVector3(Point3D value)
        {
            return new System.Numerics.Vector3(
                (float)value.X,
                (float)value.Y,
                (float)value.Z);
        }

        private static Vector3D ToVector3D(System.Numerics.Vector3 value)
        {
            return new Vector3D(value.X, value.Y, value.Z);
        }

        private static Point3D ToPoint3D(System.Numerics.Vector3 value)
        {
            return new Point3D(value.X, value.Y, value.Z);
        }
    }
}
