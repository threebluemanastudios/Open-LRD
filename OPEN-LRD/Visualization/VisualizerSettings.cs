using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OPEN_LRD.Visualization
{
    internal sealed class VisualizerSettings
    {
        public const int CurrentVersion = 1;

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        [JsonPropertyName("version")]
        public int Version { get; set; } = CurrentVersion;

        [JsonPropertyName("nodeColors")]
        public Dictionary<int, string> NodeColors { get; set; } = new Dictionary<int, string>();

        [JsonPropertyName("linkColors")]
        public Dictionary<int, string> LinkColors { get; set; } = new Dictionary<int, string>();

        [JsonPropertyName("classColors")]
        public Dictionary<int, string> ClassColors { get; set; } = new Dictionary<int, string>();

        [JsonPropertyName("view")]
        public VisualizerViewSettings View { get; set; } = new VisualizerViewSettings();

        public static string GetDefaultPath()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrWhiteSpace(root))
                root = Path.GetTempPath();

            return Path.Combine(root, "OPEN-LRD", "visualizer-settings.json");
        }

        public static VisualizerSettings CreateDefault()
        {
            return new VisualizerSettings();
        }

        public static VisualizerSettings From(GraphColorMap colors, VisualizerOptions options)
        {
            ArgumentNullException.ThrowIfNull(colors);
            ArgumentNullException.ThrowIfNull(options);

            VisualizerSettings settings = new VisualizerSettings();

            foreach ((int typeId, RgbColor color) in colors.NodeColors)
            {
                if (GraphColorPalette.TryGetName(color, out string? name) && name is not null)
                    settings.NodeColors[typeId] = name;
            }

            foreach ((int typeId, RgbColor color) in colors.LinkColors)
            {
                if (GraphColorPalette.TryGetName(color, out string? name) && name is not null)
                    settings.LinkColors[typeId] = name;
            }

            foreach ((int classId, RgbColor color) in colors.ClassColors)
            {
                if (GraphColorPalette.TryGetName(color, out string? name) && name is not null)
                    settings.ClassColors[classId] = name;
            }

            settings.View.FromOptions(options);
            return settings;
        }

        public void ApplyTo(GraphColorMap colors, VisualizerOptions options)
        {
            ArgumentNullException.ThrowIfNull(colors);
            ArgumentNullException.ThrowIfNull(options);

            Dictionary<int, RgbColor> nodeOverrides = ResolveColors(NodeColors);
            Dictionary<int, RgbColor> linkOverrides = ResolveColors(LinkColors);
            Dictionary<int, RgbColor> classOverrides = ResolveColors(ClassColors);

            colors.ReplaceColors(nodeOverrides, linkOverrides, classOverrides);
            View.ApplyTo(options);
        }

        public static VisualizerSettings Load(string path)
        {
            return Load(path, out _);
        }

        public static VisualizerSettings Load(string path, out string? warning)
        {
            ValidatePath(path);
            warning = null;

            try
            {
                if (!File.Exists(path))
                    return CreateDefault();

                string json = File.ReadAllText(path, Encoding.UTF8);
                VisualizerSettings? loaded = JsonSerializer.Deserialize<VisualizerSettings>(json, jsonOptions);

                if (loaded is null)
                {
                    warning = "Visualizer settings were empty; using defaults.";
                    return CreateDefault();
                }

                if (loaded.Version != CurrentVersion)
                {
                    warning = $"Visualizer settings version {loaded.Version} is not supported; using defaults.";
                    return CreateDefault();
                }

                loaded.NodeColors ??= new Dictionary<int, string>();
                loaded.LinkColors ??= new Dictionary<int, string>();
                loaded.ClassColors ??= new Dictionary<int, string>();
                loaded.View ??= new VisualizerViewSettings();

                return loaded;
            }
            catch (Exception ex) when (
                ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or NotSupportedException)
            {
                warning = $"Visualizer settings could not be read; using defaults. {ex.Message}";
                return CreateDefault();
            }
        }

        public void Save(string path)
        {
            ValidatePath(path);
            Version = CurrentVersion;

            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = path + ".tmp";

            try
            {
                string json = JsonSerializer.Serialize(this, jsonOptions);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        private static Dictionary<int, RgbColor> ResolveColors(
            IReadOnlyDictionary<int, string>? source)
        {
            Dictionary<int, RgbColor> colors = new Dictionary<int, RgbColor>();

            if (source is null)
                return colors;

            foreach ((int typeId, string name) in source)
            {
                if (GraphColorPalette.TryGet(name, out ColorChoice choice))
                    colors[typeId] = choice.Color;
            }

            return colors;
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Settings path cannot be empty.", nameof(path));
        }
    }

    internal sealed class VisualizerViewSettings
    {
        [JsonPropertyName("spread")]
        public double Spread { get; set; } = VisualizerOptions.DefaultSpread;

        [JsonPropertyName("cubeSize")]
        public double CubeSize { get; set; } = VisualizerOptions.DefaultCubeSize;

        [JsonPropertyName("lineWidth")]
        public double LineWidth { get; set; } = VisualizerOptions.DefaultLineWidth;

        [JsonPropertyName("showLabels")]
        public bool ShowLabels { get; set; } = true;

        [JsonPropertyName("maxLabels")]
        public int MaxLabels { get; set; } = VisualizerOptions.DefaultMaxLabels;

        [JsonPropertyName("invertMouseX")]
        public bool InvertMouseX { get; set; } = true;

        [JsonPropertyName("invertMouseY")]
        public bool InvertMouseY { get; set; } = false;

        [JsonPropertyName("nearPlaneDistance")]
        public double NearPlaneDistance { get; set; } = VisualizerOptions.DefaultNearPlaneDistance;

        public void FromOptions(VisualizerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            Spread = options.Spread;
            CubeSize = options.CubeSize;
            LineWidth = options.LineWidth;
            ShowLabels = options.ShowLabels;
            MaxLabels = options.MaxLabels;
            InvertMouseX = options.InvertMouseX;
            InvertMouseY = options.InvertMouseY;
            NearPlaneDistance = options.NearPlaneDistance;
        }

        public void ApplyTo(VisualizerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.Spread = IsPositiveFinite(Spread)
                ? Spread
                : VisualizerOptions.DefaultSpread;
            options.CubeSize = IsPositiveFinite(CubeSize)
                ? CubeSize
                : VisualizerOptions.DefaultCubeSize;
            options.LineWidth = IsPositiveFinite(LineWidth)
                ? LineWidth
                : VisualizerOptions.DefaultLineWidth;
            options.ShowLabels = ShowLabels;
            options.MaxLabels = MaxLabels > 0
                ? MaxLabels
                : VisualizerOptions.DefaultMaxLabels;
            options.InvertMouseX = InvertMouseX;
            options.InvertMouseY = InvertMouseY;
            options.NearPlaneDistance = double.IsFinite(NearPlaneDistance) && NearPlaneDistance >= 0.01 && NearPlaneDistance <= 100.0
                ? NearPlaneDistance
                : VisualizerOptions.DefaultNearPlaneDistance;
        }

        private static bool IsPositiveFinite(double value)
        {
            return double.IsFinite(value) && value > 0d;
        }
    }
}
