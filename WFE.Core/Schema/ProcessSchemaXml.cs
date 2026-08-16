using System.Collections.Generic;
using System.Xml.Serialization;

namespace WFE.Core.Schema
{
    // Root element: <Process Name="..." CanBeInlined="false" Tags="">
    [XmlRoot("Process")]
    public class ProcessSchemaXml
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        // Kept as string (not bool) because the exported XML uses "True"/"False"
        // (capital-cased), which .NET's XmlSerializer boolean parser rejects.
        // Use the *Value convenience properties below to consume as bool.
        [XmlAttribute("CanBeInlined")]
        public string CanBeInlined { get; set; }

        [XmlAttribute("Tags")]
        public string Tags { get; set; }

        [XmlElement("Designer")]
        public DesignerPositionXml Designer { get; set; }

        [XmlArray("Commands")]
        [XmlArrayItem("Command")]
        public List<CommandDefinitionXml> Commands { get; set; } = new List<CommandDefinitionXml>();

        [XmlArray("Activities")]
        [XmlArrayItem("Activity")]
        public List<ActivityDefinitionXml> Activities { get; set; } = new List<ActivityDefinitionXml>();

        [XmlArray("Transitions")]
        [XmlArrayItem("Transition")]
        public List<TransitionDefinitionXml> Transitions { get; set; } = new List<TransitionDefinitionXml>();

        // Phase 4 feature (dynamic Roslyn-compiled actions) - modeled now, executed later.
        [XmlArray("CodeActions")]
        [XmlArrayItem("CodeAction")]
        public List<CodeActionDefinitionXml> CodeActions { get; set; } = new List<CodeActionDefinitionXml>();

        [XmlIgnore]
        public bool CanBeInlinedValue => ParseBool(CanBeInlined);

        internal static bool ParseBool(string value) =>
            !string.IsNullOrEmpty(value) && bool.TryParse(value, out var b) && b;
    }

    public class CommandDefinitionXml
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }
    }

    public class DesignerPositionXml
    {
        [XmlAttribute("X")]
        public double X { get; set; }

        [XmlAttribute("Y")]
        public double Y { get; set; }

        [XmlAttribute("Hidden")]
        public string Hidden { get; set; }
    }

    public class ActivityDefinitionXml
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlAttribute("State")]
        public string State { get; set; }

        [XmlAttribute("IsInitial")]
        public string IsInitial { get; set; }

        [XmlAttribute("IsFinal")]
        public string IsFinal { get; set; }

        [XmlAttribute("IsForSetState")]
        public string IsForSetState { get; set; }

        [XmlAttribute("IsAutoSchemeUpdate")]
        public string IsAutoSchemeUpdate { get; set; }

        [XmlAttribute("WasInlined")]
        public string WasInlined { get; set; }

        [XmlAttribute("OriginalName")]
        public string OriginalName { get; set; }

        [XmlAttribute("OriginalSchemeCode")]
        public string OriginalSchemeCode { get; set; }

        [XmlElement("Implementation")]
        public ImplementationXml Implementation { get; set; }

        [XmlElement("Designer")]
        public DesignerPositionXml Designer { get; set; }

        [XmlIgnore]
        public bool IsInitialValue => ProcessSchemaXml.ParseBool(IsInitial);

        [XmlIgnore]
        public bool IsFinalValue => ProcessSchemaXml.ParseBool(IsFinal);
    }

    public class ImplementationXml
    {
        [XmlElement("ActionRef")]
        public List<ActionRefXml> ActionRefs { get; set; } = new List<ActionRefXml>();
    }

    public class ActionRefXml
    {
        [XmlAttribute("Order")]
        public int Order { get; set; }

        [XmlAttribute("NameRef")]
        public string NameRef { get; set; }

        // Raw JSON payload (arrives as CDATA text content, XmlSerializer reads it as plain text).
        [XmlElement("ActionParameter")]
        public string ActionParameter { get; set; }
    }

    public class TransitionDefinitionXml
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlAttribute("To")]
        public string To { get; set; }

        [XmlAttribute("From")]
        public string From { get; set; }

        // Direct | NotSpecified
        [XmlAttribute("Classifier")]
        public string Classifier { get; set; }

        [XmlAttribute("AllowConcatenationType")]
        public string AllowConcatenationType { get; set; }

        [XmlAttribute("RestrictConcatenationType")]
        public string RestrictConcatenationType { get; set; }

        // And | Or - how multiple <Condition> entries combine
        [XmlAttribute("ConditionsConcatenationType")]
        public string ConditionsConcatenationType { get; set; }

        [XmlAttribute("DisableParentStateControl")]
        public string DisableParentStateControl { get; set; }

        // --- Subprocess / fork support (Phase 3) ---
        [XmlAttribute("IsFork")]
        public string IsFork { get; set; }

        [XmlAttribute("SubprocessInOutDefinition")]
        public string SubprocessInOutDefinition { get; set; }

        [XmlAttribute("SubprocessStartupType")]
        public string SubprocessStartupType { get; set; }

        [XmlAttribute("SubprocessStartupParameterCopyStrategy")]
        public string SubprocessStartupParameterCopyStrategy { get; set; }

        [XmlAttribute("SubprocessFinalizeParameterMergeStrategy")]
        public string SubprocessFinalizeParameterMergeStrategy { get; set; }

        [XmlArray("Triggers")]
        [XmlArrayItem("Trigger")]
        public List<TriggerXml> Triggers { get; set; } = new List<TriggerXml>();

        [XmlArray("Conditions")]
        [XmlArrayItem("Condition")]
        public List<ConditionXml> Conditions { get; set; } = new List<ConditionXml>();

        [XmlElement("Designer")]
        public DesignerPositionXml Designer { get; set; }

        [XmlIgnore]
        public bool IsForkValue => ProcessSchemaXml.ParseBool(IsFork);
    }

    public class TriggerXml
    {
        // Auto | Command | Schedule
        [XmlAttribute("Type")]
        public string Type { get; set; }

        // Only present when Type == Command; references a <Command Name="..."/>
        [XmlAttribute("NameRef")]
        public string NameRef { get; set; }

        // Only present when Type == Schedule - raw JSON payload, e.g.
        // {"Mode":"Interval","IntervalSeconds":30} or {"Mode":"TargetDateTime","ParameterName":"NextRunTime"}
        // (see WFE.Core.Runtime.ScheduleTriggerConfig). Not part of any of your sample XMLs -
        // this shape is my own design, invented for this engine.
        [XmlElement("ScheduleParameter")]
        public string ScheduleParameter { get; set; }
    }

    public class ConditionXml
    {
        // Always | Otherwise | Expression | Action
        [XmlAttribute("Type")]
        public string Type { get; set; }

        [XmlAttribute("ConditionInversion")]
        public string ConditionInversion { get; set; }

        // Only present when Type == Action; references a registered ICondition
        [XmlAttribute("NameRef")]
        public string NameRef { get; set; }

        // Only present when Type == Expression
        [XmlElement("Expression")]
        public string Expression { get; set; }

        // Only present when Type == Action - raw JSON payload
        [XmlElement("ActionParameter")]
        public string ActionParameter { get; set; }

        [XmlIgnore]
        public bool IsInverted => ProcessSchemaXml.ParseBool(ConditionInversion);
    }

    public class CodeActionDefinitionXml
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlAttribute("Type")]
        public string Type { get; set; }

        [XmlAttribute("IsGlobal")]
        public string IsGlobal { get; set; }

        [XmlAttribute("IsAsync")]
        public string IsAsync { get; set; }

        [XmlAttribute("WasInlined")]
        public string WasInlined { get; set; }

        [XmlAttribute("OriginalName")]
        public string OriginalName { get; set; }

        [XmlAttribute("OriginalSchemeCode")]
        public string OriginalSchemeCode { get; set; }

        [XmlElement("ActionCode")]
        public string ActionCode { get; set; }

        [XmlElement("Usings")]
        public string Usings { get; set; }

        [XmlArray("Parameters")]
        [XmlArrayItem("Parameter")]
        public List<CodeActionParameterXml> Parameters { get; set; } = new List<CodeActionParameterXml>();

        [XmlIgnore]
        public bool IsAsyncValue => ProcessSchemaXml.ParseBool(IsAsync);
    }

    public class CodeActionParameterXml
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("isRequired")]
        public string IsRequired { get; set; }

        [XmlElement("DefaultValue")]
        public string DefaultValue { get; set; }
    }
}
