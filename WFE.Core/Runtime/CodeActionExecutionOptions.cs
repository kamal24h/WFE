namespace WFE.Core.Runtime
{
    public class CodeActionExecutionOptions
    {
        /// <summary>Off by default. CodeActions compile and run arbitrary C# with FULL
        /// application privileges - file system, network, everything this process can do.
        /// There is no sandboxing (the same is true of every CodeAction-style feature in every
        /// workflow engine that supports this pattern, including the legacy WfeRuntime.cs you
        /// shared - Roslyn-compiled code shares the host process's trust boundary, full stop).
        /// Only enable this if you trust everyone who can author or import a workflow schema
        /// as much as you'd trust someone with shell access to this server.</summary>
        public bool Enabled { get; set; } = false;
    }
}
