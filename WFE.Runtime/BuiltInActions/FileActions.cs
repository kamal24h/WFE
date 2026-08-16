using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WFE.Core.Actions;
using WFE.Core.Runtime;

namespace WFE.Runtime.BuiltInActions
{
    public class FileWriteArgs
    {
        [JsonPropertyName("FileMode")] public string FileMode { get; set; } = "OpenOrCreate";
        [JsonPropertyName("FileAccess")] public string FileAccess { get; set; } = "Write";
        [JsonPropertyName("FileShare")] public string FileShare { get; set; } = "None";
        [JsonPropertyName("Path")] public string Path { get; set; }
        [JsonPropertyName("Text")] public string Text { get; set; }
    }

    /// <summary>&lt;ActionRef NameRef="FileWrite"&gt; - matches FileWriteRead.xml.</summary>
    public class FileWriteAction : IActionExecutor
    {
        private readonly FileActionOptions _options;

        public FileWriteAction(FileActionOptions options)
        {
            _options = options;
        }

        public string Name => "FileWrite";

        public async Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<FileWriteArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var fullPath = FilePathResolver.Resolve(_options.RootPath, args.Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath) ?? ".");

            var fileMode = Enum.Parse<System.IO.FileMode>(args.FileMode, ignoreCase: true);
            var fileAccess = Enum.Parse<System.IO.FileAccess>(args.FileAccess, ignoreCase: true);
            var fileShare = Enum.Parse<System.IO.FileShare>(args.FileShare, ignoreCase: true);

            await using var stream = new FileStream(fullPath, fileMode, fileAccess, fileShare, bufferSize: 4096, useAsync: true);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(args.Text ?? string.Empty);
        }
    }

    public class FileReadArgs
    {
        [JsonPropertyName("ParameterPurpose")] public string ParameterPurpose { get; set; }
        [JsonPropertyName("ParameterType")] public string ParameterType { get; set; }
        [JsonPropertyName("FileMode")] public string FileMode { get; set; } = "Open";
        [JsonPropertyName("FileAccess")] public string FileAccess { get; set; } = "Read";
        [JsonPropertyName("FileShare")] public string FileShare { get; set; } = "None";
        [JsonPropertyName("BufferSize")] public int BufferSize { get; set; } = 4096;
        [JsonPropertyName("Path")] public string Path { get; set; }
        [JsonPropertyName("ParameterName")] public string ParameterName { get; set; }
    }

    /// <summary>&lt;ActionRef NameRef="FileRead"&gt; - matches FileWriteRead.xml. Only
    /// ParameterType "String" is implemented; other types (Number/DateTime/etc, if your
    /// designer emits them) will need their own parsing branch added here.</summary>
    public class FileReadAction : IActionExecutor
    {
        private readonly FileActionOptions _options;

        public FileReadAction(FileActionOptions options)
        {
            _options = options;
        }

        public string Name => "FileRead";

        public async Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<FileReadArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var fullPath = FilePathResolver.Resolve(_options.RootPath, args.Path);

            var fileMode = Enum.Parse<System.IO.FileMode>(args.FileMode, ignoreCase: true);
            var fileAccess = Enum.Parse<System.IO.FileAccess>(args.FileAccess, ignoreCase: true);
            var fileShare = Enum.Parse<System.IO.FileShare>(args.FileShare, ignoreCase: true);

            string text;
            await using (var stream = new FileStream(fullPath, fileMode, fileAccess, fileShare, args.BufferSize, useAsync: true))
            using (var reader = new StreamReader(stream))
            {
                text = await reader.ReadToEndAsync();
            }

            await context.Parameters.SetAsync(context.Instance.Id, args.ParameterName, text);
        }
    }

    public class FileDeleteArgs
    {
        [JsonPropertyName("Path")] public string Path { get; set; }
    }

    /// <summary>&lt;ActionRef NameRef="FileDelete"&gt; - matches FileWriteRead.xml. Idempotent:
    /// deleting an already-absent file is not an error (a retry after a partial failure
    /// shouldn't fault the instance just because the file was already gone).</summary>
    public class FileDeleteAction : IActionExecutor
    {
        private readonly FileActionOptions _options;

        public FileDeleteAction(FileActionOptions options)
        {
            _options = options;
        }

        public string Name => "FileDelete";

        public Task ExecuteAsync(WorkflowExecutionContext context, string rawJsonParameters)
        {
            var args = JsonSerializer.Deserialize<FileDeleteArgs>(rawJsonParameters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var fullPath = FilePathResolver.Resolve(_options.RootPath, args.Path);
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            return Task.CompletedTask;
        }
    }
}
