using System;
using System.IO;

namespace WFE.Runtime.BuiltInActions
{
    public class FileActionOptions
    {
        /// <summary>All FileWrite/FileRead/FileDelete Path values are resolved relative to
        /// this root - defaults to a "wfe-files" folder under the app's base directory if not
        /// set in config. A schema-supplied Path can never escape this root (see
        /// FilePathResolver.Resolve) - without that check, a schema with "Path":"../../../etc/passwd"
        /// would let file actions read/write/delete arbitrary files on the host.</summary>
        public string RootPath { get; set; }
    }

    internal static class FilePathResolver
    {
        public static string Resolve(string rootPath, string requestedPath)
        {
            var root = Path.GetFullPath(string.IsNullOrWhiteSpace(rootPath)
                ? Path.Combine(AppContext.BaseDirectory, "wfe-files")
                : rootPath);

            var combined = Path.GetFullPath(Path.Combine(root, requestedPath ?? string.Empty));

            if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException(
                    $"Path '{requestedPath}' resolves outside the allowed file action root '{root}'.");

            return combined;
        }
    }
}
