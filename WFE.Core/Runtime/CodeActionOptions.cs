namespace WFE.Core.Runtime
{
    /// <summary>
    /// Dynamic CodeActions compile and execute arbitrary C# embedded in the schema XML itself -
    /// this is intentionally disabled by default. Enabling it (CodeActions:Enabled=true in
    /// appsettings.json) means anyone who can get a scheme published through
    /// SchemeDesignerController can run arbitrary code with the full permissions of the
    /// WFE.Web process. Modern .NET has no in-process sandboxing (no AppDomain/CAS like old
    /// .NET Framework) - there is no code-level boundary here, only whatever access control you
    /// put in front of the scheme-publish endpoint. Only enable this if that endpoint is
    /// properly authenticated/authorized and restricted to people you'd trust to write code
    /// that runs on this server.
    /// </summary>
    public class CodeActionOptions
    {
        public bool Enabled { get; set; } = false;
    }
}
