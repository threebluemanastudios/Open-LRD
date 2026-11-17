using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using OPEN_LRD.Visualization;

namespace OPEN_LRD.Visualizer
{
    /// <summary>Stores a prepared WPF mesh batch for one node type.</summary>
    internal sealed record PreparedNodeMeshBatch(int NodeTypeId, MeshGeometry3D Mesh);

    /// <summary>Stores a prepared WPF mesh batch for one forward relationship type and class.</summary>
    internal sealed record PreparedLinkMeshBatch(int ForwardTypeId, int ClassId, MeshGeometry3D Mesh);

    /// <summary>Contains prepared node/link meshes and the layout bounds used for camera framing.</summary>
    internal sealed class PreparedSceneGeometry
    {
        /// <summary>Creates a prepared scene geometry container from frozen WPF mesh batches and bounds.</summary>
        public PreparedSceneGeometry(
            IReadOnlyList<PreparedNodeMeshBatch> nodeBatches,
            IReadOnlyList<PreparedLinkMeshBatch> linkBatches,
            Vector3 center,
            float radius)
        {
            NodeBatches = nodeBatches;
            LinkBatches = linkBatches;
            Center = center;
            Radius = radius;
        }

        public IReadOnlyList<PreparedNodeMeshBatch> NodeBatches { get; }
        public IReadOnlyList<PreparedLinkMeshBatch> LinkBatches { get; }
        public Vector3 Center { get; }
        public float Radius { get; }
    }

    /// <summary>Manages WPF 3D scene models, camera state, visibility, and node picking.</summary>
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
        private readonly Dictionary<int, GeometryModel3D> nodeModels = new Dictionary<int, GeometryModel3D>();
        private readonly Dictionary<int, (GeometryModel3D Model, int ClassId)> linkModels = new Dictionary<int, (GeometryModel3D, int)>();
        private readonly HashSet<int> hiddenNodeTypes = new HashSet<int>();
        private readonly HashSet<int> hiddenLinkTypes = new HashSet<int>();
        private readonly FlyCamera flyCamera = new FlyCamera();

        private GeometryModel3D? selectedHighlightModel;
        private GeometryModel3D? hoveredHighlightModel;
        private Vector3 center;
        private float radius;

        /// <summary>Initializes the 3D scene camera, lighting, and model group on the target viewport.</summary>
        public VisualizerScene(Viewport3D viewport)
        {
            this.viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));

            camera.Position = new Point3D(0.0, 0.0, -10.0);
            camera.LookDirection = InitialLookDirection;
            viewport.Camera = camera;

            SyncFlyCameraFromCamera();

            sceneGroup.Children.Add(new AmbientLight(Color.FromRgb(90, 90, 90)));
            sceneGroup.Children.Add(new DirectionalLight(
                Color.FromRgb(230, 230, 230),
                new Vector3D(1.0, -1.0, -1.0)));
            sceneGroup.Children.Add(new DirectionalLight(
                Color.FromRgb(140, 155, 180),
                new Vector3D(-1.0, 0.5, 1.0)));

            viewport.Children.Add(new ModelVisual3D { Content = sceneGroup });
        }

        public FlyCamera FlyCamera => flyCamera;

        public float Radius => radius;

        public Vector3 Center => center;

        public bool HasGeometry => geometryModels.Count > 0;

        public int GeometryModelCount => geometryModels.Count;

        public double NearPlaneDistance
        {
            get => camera.NearPlaneDistance;
            set => camera.NearPlaneDistance = Math.Clamp(value, 0.01, 100.0);
        }

        /// <summary>Determines whether meshes for the specified node type ID are currently visible.</summary>
        public bool IsNodeTypeVisible(int nodeTypeId) => !hiddenNodeTypes.Contains(nodeTypeId);

        /// <summary>Determines whether meshes for the specified forward link type ID are currently visible.</summary>
        public bool IsLinkTypeVisible(int forwardTypeId) => !hiddenLinkTypes.Contains(forwardTypeId);

        /// <summary>Shows or hides the 3D model batch for the specified node type ID.</summary>
        public void SetNodeTypeVisible(int nodeTypeId, bool visible)
        {
            if (visible)
            {
                hiddenNodeTypes.Remove(nodeTypeId);
                if (nodeModels.TryGetValue(nodeTypeId, out GeometryModel3D? model) && !sceneGroup.Children.Contains(model))
                {
                    sceneGroup.Children.Add(model);
                }
            }
            else
            {
                hiddenNodeTypes.Add(nodeTypeId);
                if (nodeModels.TryGetValue(nodeTypeId, out GeometryModel3D? model) && sceneGroup.Children.Contains(model))
                {
                    sceneGroup.Children.Remove(model);
                }
            }
        }

        /// <summary>Shows or hides the 3D model batch for the specified forward link type ID.</summary>
        public void SetLinkTypeVisible(int forwardTypeId, bool visible)
        {
            if (visible)
            {
                hiddenLinkTypes.Remove(forwardTypeId);
                if (linkModels.TryGetValue(forwardTypeId, out var tuple) && !sceneGroup.Children.Contains(tuple.Model))
                {
                    sceneGroup.Children.Add(tuple.Model);
                }
            }
            else
            {
                hiddenLinkTypes.Add(forwardTypeId);
                if (linkModels.TryGetValue(forwardTypeId, out var tuple) && sceneGroup.Children.Contains(tuple.Model))
                {
                    sceneGroup.Children.Remove(tuple.Model);
                }
            }
        }

        /// <summary>Converts raw scene geometry into frozen WPF 3D mesh batches ready for UI attachment.</summary>
        public static PreparedSceneGeometry PrepareGeometry(SceneGeometry geometry)
        {
            ArgumentNullException.ThrowIfNull(geometry);

            List<PreparedNodeMeshBatch> nodeBatches = new List<PreparedNodeMeshBatch>(geometry.NodeBatches.Count);
            foreach (NodeMeshBatch batch in geometry.NodeBatches)
                nodeBatches.Add(new PreparedNodeMeshBatch(batch.NodeTypeId, CreateMesh(batch.Mesh)));

            List<PreparedLinkMeshBatch> linkBatches = new List<PreparedLinkMeshBatch>(geometry.LinkBatches.Count);
            foreach (LinkMeshBatch batch in geometry.LinkBatches)
                linkBatches.Add(new PreparedLinkMeshBatch(batch.ForwardTypeId, batch.ClassId, CreateMesh(batch.Mesh)));

            return new PreparedSceneGeometry(nodeBatches, linkBatches, geometry.Center, geometry.Radius);
        }

        /// <summary>Prepares and applies the specified scene geometry and color palette to the viewport.</summary>
        public void Apply(SceneGeometry geometry, GraphColorMap colors)
        {
            Apply(PrepareGeometry(geometry), colors);
        }

        /// <summary>Replaces the active scene models with the prepared geometry batches and frames the graph.</summary>
        public void Apply(PreparedSceneGeometry geometry, GraphColorMap colors)
        {
            ArgumentNullException.ThrowIfNull(geometry);
            ArgumentNullException.ThrowIfNull(colors);

            ClearGeometry();

            foreach (PreparedNodeMeshBatch batch in geometry.NodeBatches)
            {
                Material material = CreateNodeMaterial(colors.GetNodeColor(batch.NodeTypeId));
                GeometryModel3D? model = AddModel(batch.Mesh, material, backMaterial: null);
                if (model is not null)
                {
                    nodeModels[batch.NodeTypeId] = model;
                    if (hiddenNodeTypes.Contains(batch.NodeTypeId))
                        sceneGroup.Children.Remove(model);
                }
            }

            foreach (PreparedLinkMeshBatch batch in geometry.LinkBatches)
            {
                Material material = CreateLinkMaterial(
                    colors.GetLinkColor(batch.ForwardTypeId, batch.ClassId));
                GeometryModel3D? model = AddModel(batch.Mesh, material, material);
                if (model is not null)
                {
                    linkModels[batch.ForwardTypeId] = (model, batch.ClassId);
                    if (hiddenLinkTypes.Contains(batch.ForwardTypeId))
                        sceneGroup.Children.Remove(model);
                }
            }

            center = geometry.Center;
            radius = geometry.Radius;
            FrameAll();
        }

        /// <summary>Rebuilds node and link materials from the current color map overrides.</summary>
        public void UpdateMaterials(GraphColorMap colors)
        {
            ArgumentNullException.ThrowIfNull(colors);

            foreach ((int nodeTypeId, GeometryModel3D model) in nodeModels)
            {
                model.Material = CreateNodeMaterial(colors.GetNodeColor(nodeTypeId));
            }

            foreach ((int forwardTypeId, (GeometryModel3D model, int classId)) in linkModels)
            {
                Material material = CreateLinkMaterial(colors.GetLinkColor(forwardTypeId, classId));
                model.Material = material;
                model.BackMaterial = material;
            }
        }

        /// <summary>Removes all scene geometry and resets the bounds state.</summary>
        public void Clear()
        {
            ClearGeometry();
            center = default;
            radius = 0f;
            SyncFlyCameraFromCamera();
        }

        /// <summary>Positions the camera to frame the entire scene bounding sphere.</summary>
        public void FrameAll()
        {
            if (!HasGeometry)
                return;

            Frame(center, Math.Max(radius, 0.5f));
            SyncFlyCameraFromCamera();
        }

        /// <summary>Positions the camera to frame a specific target point and radius.</summary>
        public void FrameTarget(Vector3 target, float targetRadius)
        {
            Frame(target, Math.Max(targetRadius, 0.5f));
            SyncFlyCameraFromCamera();
        }

        /// <summary>Resets the camera orientation to its default angle and frames the scene.</summary>
        public void ResetView()
        {
            camera.UpDirection = new Vector3D(0.0, 1.0, 0.0);
            camera.LookDirection = InitialLookDirection;
            FrameAll();
        }

        /// <summary>Captures the current WPF camera and viewport dimensions as a projection view.</summary>
        public CameraView GetCameraView()
        {
            double width = viewport.ActualWidth > 1.0 ? viewport.ActualWidth : 1600.0;
            double height = viewport.ActualHeight > 1.0 ? viewport.ActualHeight : 900.0;

            return new CameraView(
                ToVector3(camera.Position),
                ToVector3(camera.LookDirection),
                ToVector3(camera.UpDirection),
                camera.FieldOfView,
                width,
                height,
                camera.NearPlaneDistance,
                camera.FarPlaneDistance);
        }

        /// <summary>Updates the WPF perspective camera from the fly-camera state.</summary>
        public void SyncCameraFromFlyCamera()
        {
            camera.Position = ToPoint3D(flyCamera.Position);
            camera.LookDirection = ToVector3D(flyCamera.LookDirection);
            camera.UpDirection = ToVector3D(flyCamera.Up);
        }

        /// <summary>Updates the fly-camera pose from the current WPF perspective camera.</summary>
        public void SyncFlyCameraFromCamera()
        {
            Vector3 look = ToVector3(camera.LookDirection);
            Vector3 up = ToVector3(camera.UpDirection);
            if (look.LengthSquared() > 1e-12f && up.LengthSquared() > 1e-12f)
                flyCamera.SetOrientation(ToVector3(camera.Position), look, up);
        }

        /// <summary>Shows or clears the highlight cube for the currently selected node.</summary>
        public void SetSelectedHighlight(Vector3? position, double cubeSize)
        {
            if (selectedHighlightModel is not null)
            {
                sceneGroup.Children.Remove(selectedHighlightModel);
                selectedHighlightModel = null;
            }

            if (!position.HasValue)
                return;

            MeshData meshData = GraphMeshBuilder.BuildHighlightCube(position.Value, cubeSize);
            Material material = CreateSelectionHighlightMaterial();
            selectedHighlightModel = new GeometryModel3D(CreateMesh(meshData), material);
            selectedHighlightModel.Freeze();
            sceneGroup.Children.Add(selectedHighlightModel);
        }

        /// <summary>Shows or clears the highlight cube for the currently hovered node.</summary>
        public void SetHoveredHighlight(Vector3? position, double cubeSize)
        {
            if (hoveredHighlightModel is not null)
            {
                sceneGroup.Children.Remove(hoveredHighlightModel);
                hoveredHighlightModel = null;
            }

            if (!position.HasValue)
                return;

            MeshData meshData = GraphMeshBuilder.CreateCube(position.Value, cubeSize * 1.08);
            Material material = CreateHoverHighlightMaterial();
            hoveredHighlightModel = new GeometryModel3D(CreateMesh(meshData), material);
            hoveredHighlightModel.Freeze();
            sceneGroup.Children.Add(hoveredHighlightModel);
        }

        /// <summary>Creates the cyan emissive-and-specular material used for selected node highlights.</summary>
        private static Material CreateSelectionHighlightMaterial()
        {
            MaterialGroup group = new MaterialGroup();
            SolidColorBrush brush = CreateBrush(new RgbColor(79, 195, 247)); // Bright cyan
            SolidColorBrush emissiveBrush = CreateBrush(new RgbColor(50, 140, 200));
            group.Children.Add(new EmissiveMaterial(emissiveBrush));
            group.Children.Add(new DiffuseMaterial(brush));
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 60.0));
            group.Freeze();
            return group;
        }

        /// <summary>Creates the amber emissive-and-diffuse material used for hovered node highlights.</summary>
        private static Material CreateHoverHighlightMaterial()
        {
            MaterialGroup group = new MaterialGroup();
            SolidColorBrush brush = CreateBrush(new RgbColor(255, 213, 79)); // Amber / Gold
            SolidColorBrush emissiveBrush = CreateBrush(new RgbColor(160, 130, 40));
            group.Children.Add(new EmissiveMaterial(emissiveBrush));
            group.Children.Add(new DiffuseMaterial(brush));
            group.Freeze();
            return group;
        }

        /// <summary>Updates the WPF camera position and clip planes to fit the target sphere.</summary>
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

        /// <summary>Creates and adds a 3D geometry model to the scene when the mesh is non-empty.</summary>
        private GeometryModel3D? AddModel(MeshGeometry3D mesh, Material material, Material? backMaterial)
        {
            if (mesh.TriangleIndices.Count == 0)
                return null;

            GeometryModel3D model = new GeometryModel3D(mesh, material)
            {
                BackMaterial = backMaterial
            };
            geometryModels.Add(model);
            sceneGroup.Children.Add(model);
            return model;
        }

        /// <summary>Removes all node, link, and highlight models from the scene group.</summary>
        private void ClearGeometry()
        {
            if (selectedHighlightModel is not null)
            {
                sceneGroup.Children.Remove(selectedHighlightModel);
                selectedHighlightModel = null;
            }

            if (hoveredHighlightModel is not null)
            {
                sceneGroup.Children.Remove(hoveredHighlightModel);
                hoveredHighlightModel = null;
            }

            foreach (GeometryModel3D model in geometryModels)
                sceneGroup.Children.Remove(model);

            geometryModels.Clear();
            nodeModels.Clear();
            linkModels.Clear();
        }

        /// <summary>Builds a frozen WPF 3D mesh from indexed position, normal, and triangle arrays.</summary>
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

        /// <summary>Creates a frozen diffuse-and-specular material for a node batch.</summary>
        private static Material CreateNodeMaterial(RgbColor color)
        {
            MaterialGroup group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(CreateBrush(color)));
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 40.0));
            group.Freeze();
            return group;
        }

        /// <summary>Creates a frozen emissive-and-diffuse material for a link batch.</summary>
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

        /// <summary>Creates a frozen solid-color brush from an RGB color value.</summary>
        private static SolidColorBrush CreateBrush(RgbColor color)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }

        /// <summary>Returns a normalized copy of a WPF 3D vector.</summary>
        private static Vector3D Normalize(Vector3D value)
        {
            value.Normalize();
            return value;
        }

        /// <summary>Converts a WPF 3D vector into a single-precision numeric vector.</summary>
        private static System.Numerics.Vector3 ToVector3(Vector3D value)
        {
            return new System.Numerics.Vector3(
                (float)value.X,
                (float)value.Y,
                (float)value.Z);
        }

        /// <summary>Converts a WPF 3D point into a single-precision numeric vector.</summary>
        private static System.Numerics.Vector3 ToVector3(Point3D value)
        {
            return new System.Numerics.Vector3(
                (float)value.X,
                (float)value.Y,
                (float)value.Z);
        }

        /// <summary>Converts a single-precision numeric vector into a WPF 3D vector.</summary>
        private static Vector3D ToVector3D(System.Numerics.Vector3 value)
        {
            return new Vector3D(value.X, value.Y, value.Z);
        }

        /// <summary>Converts a single-precision numeric vector into a WPF 3D point.</summary>
        private static Point3D ToPoint3D(System.Numerics.Vector3 value)
        {
            return new Point3D(value.X, value.Y, value.Z);
        }
    }
}
