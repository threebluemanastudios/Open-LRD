using System.IO;

namespace OPEN_LRD.Nodes
{
    /// <summary>Identifies an external payload by a validated absolute URI and optional metadata.</summary>
    public sealed record DataPointer
    {
        /// <summary>Gets the absolute URI where the referenced payload is located.</summary>
        public Uri Location { get; }
        /// <summary>Gets the optional media type associated with the payload.</summary>
        public string? MediaType { get; }
        /// <summary>Gets the optional payload size in bytes.</summary>
        public long? ByteLength { get; }
        /// <summary>Gets the optional checksum used to identify a payload revision.</summary>
        public string? Checksum { get; }

        /// <summary>Creates a pointer after validating its URI scheme and metadata.</summary>
        /// <param name="location">An absolute file, HTTP, or HTTPS URI.</param>
        /// <param name="mediaType">An optional MIME type.</param>
        /// <param name="byteLength">An optional non-negative payload size.</param>
        /// <param name="checksum">An optional payload checksum.</param>
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

        /// <summary>Creates a file pointer from a path after converting it to an absolute path.</summary>
        /// <param name="path">The local file path to reference.</param>
        /// <returns>A pointer whose location uses the file URI scheme.</returns>
        public static DataPointer FromFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("File path cannot be empty.", nameof(path));

            return new DataPointer(new Uri(Path.GetFullPath(path)));
        }
    }
}
