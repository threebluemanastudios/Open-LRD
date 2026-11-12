using System.IO;

namespace OPEN_LRD.Nodes
{
    public sealed record DataPointer
    {
        public Uri Location { get; }
        public string? MediaType { get; }
        public long? ByteLength { get; }
        public string? Checksum { get; }

        public DataPointer(Uri location, string? mediaType = null, long? byteLength = null, string? checksum = null)
        {
            ArgumentNullException.ThrowIfNull(location);

            if (!location.IsAbsoluteUri)
                throw new ArgumentException("Data pointer location must be an absolute URI.", nameof(location));

            string scheme = location.Scheme.ToLowerInvariant();
            if (scheme != "file" && scheme != "http" && scheme != "https")
                throw new ArgumentException("Data pointer scheme must be file, http, or https.", nameof(location));

            if (!string.IsNullOrEmpty(location.UserInfo))
                throw new ArgumentException("Data pointer location must not contain credentials.", nameof(location));

            if (byteLength is < 0)
                throw new ArgumentOutOfRangeException(nameof(byteLength), "Data pointer byte length cannot be negative.");

            Location = location;
            MediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim();
            ByteLength = byteLength;
            Checksum = string.IsNullOrWhiteSpace(checksum) ? null : checksum.Trim();
        }

        public static DataPointer FromFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("File path cannot be empty.", nameof(path));

            return new DataPointer(new Uri(Path.GetFullPath(path)));
        }
    }
}
