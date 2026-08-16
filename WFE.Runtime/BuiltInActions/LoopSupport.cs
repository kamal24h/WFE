namespace WFE.Runtime.BuiltInActions
{
    /// <summary>
    /// "Broken" is modeled now even though no sample XML shows a NameRef that sets it (there's
    /// presumably a "BreakLoop" action in the designer we haven't seen a sample for) - adding
    /// one later needs zero changes to LoopIsNotCompletedAndBrokenCondition, since it already
    /// just checks "!= InProgress".
    /// </summary>
    internal enum LoopState
    {
        InProgress,
        Completed,
        Broken
    }

    /// <summary>
    /// Internal bookkeeping parameters are namespaced under a prefix that a real schema
    /// parameter name is very unlikely to collide with, and are SEPARATE from the
    /// LoopStateParameterName/LoopCounterValueParameterName the schema configures - those are
    /// the user-facing aliases (for @LoopCounterValue-style expressions inside the loop body);
    /// these internal keys are what LoopIsNotCompletedAndBroken actually relies on, since it's
    /// only ever given a LoopName, not the parameter names the designer happened to pick.
    /// </summary>
    internal static class LoopKeys
    {
        public static string State(string loopName) => $"__wfe_loop__{loopName}__State";
        public static string CurrentValue(string loopName) => $"__wfe_loop__{loopName}__CurrentValue";
        public static string Index(string loopName) => $"__wfe_loop__{loopName}__Index";
        public static string ValuesJson(string loopName) => $"__wfe_loop__{loopName}__ValuesJson";
    }
}
