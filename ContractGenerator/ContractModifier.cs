using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ACS_4Series_Template_V3.ContractGenerator
{
    /// <summary>
    /// Utility class to generate and modify CH5 contracts without using Crestron's Contract Editor.
    /// This allows programmatic creation of .cse2j JSON files and .g.cs C# files.
    /// Note: This version uses simple string manipulation to avoid external JSON dependencies.
    /// </summary>
    public class ContractModifier
    {
        #region Component Definition

        /// <summary>
        /// Defines a contract component (like SubsystemButton, MusicRoomControl, etc.)
        /// </summary>
        public class ComponentDefinition
        {
            public string Name { get; set; }
            public string Namespace { get; set; }
            public int InstanceCount { get; set; }
            public uint StartSmartObjectId { get; set; }
            public List<SignalDefinition> BooleanInputs { get; set; } = new List<SignalDefinition>();
            public List<SignalDefinition> BooleanOutputs { get; set; } = new List<SignalDefinition>();
            public List<SignalDefinition> NumericInputs { get; set; } = new List<SignalDefinition>();
            public List<SignalDefinition> NumericOutputs { get; set; } = new List<SignalDefinition>();
            public List<SignalDefinition> StringInputs { get; set; } = new List<SignalDefinition>();
            public List<SignalDefinition> StringOutputs { get; set; } = new List<SignalDefinition>();
        }

        public class SignalDefinition
        {
            public string Name { get; set; }
            public uint JoinNumber { get; set; }
        }

        #endregion

        #region Generate JSON Snippet for Component

        /// <summary>
        /// Generates the JSON snippet to add to ACS_Contract.txt for a new component.
        /// You'll need to manually insert this into the appropriate sections of the JSON file.
        /// </summary>
        public static string GenerateJsonSnippet(
            string componentName,
            uint startSmartObjectId,
            int count,
            Dictionary<uint, string> booleanStates = null,
            Dictionary<uint, string> booleanEvents = null,
            Dictionary<uint, string> numericStates = null,
            Dictionary<uint, string> numericEvents = null,
            Dictionary<uint, string> stringStates = null,
            Dictionary<uint, string> stringEvents = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// === ADD TO ACS_Contract.txt ===");
            sb.AppendLine();

            // Boolean states
            if (booleanStates != null && booleanStates.Count > 0)
            {
                sb.AppendLine("// Add to \"signals\" > \"states\" > \"boolean\":");
                for (int i = 0; i < count; i++)
                {
                    uint smartObjId = startSmartObjectId + (uint)i;
                    sb.AppendLine($"\t\t\t\t\"{smartObjId}\": {{");
                    var entries = booleanStates.Select(kvp => $"\t\t\t\t\t\"{kvp.Key}\": \"{componentName}[{i}].{kvp.Value}\"");
                    sb.AppendLine(string.Join(",\n", entries));
                    sb.AppendLine(i < count - 1 ? "\t\t\t\t}," : "\t\t\t\t}");
                }
                sb.AppendLine();
            }

            // Boolean events
            if (booleanEvents != null && booleanEvents.Count > 0)
            {
                sb.AppendLine("// Add to \"signals\" > \"events\" > \"boolean\":");
                for (int i = 0; i < count; i++)
                {
                    uint smartObjId = startSmartObjectId + (uint)i;
                    sb.AppendLine($"\t\t\t\t\"{smartObjId}\": {{");
                    var entries = booleanEvents.Select(kvp => $"\t\t\t\t\t\"{kvp.Key}\": \"{componentName}[{i}].{kvp.Value}\"");
                    sb.AppendLine(string.Join(",\n", entries));
                    sb.AppendLine(i < count - 1 ? "\t\t\t\t}," : "\t\t\t\t}");
                }
                sb.AppendLine();
            }

            // Numeric states
            if (numericStates != null && numericStates.Count > 0)
            {
                sb.AppendLine("// Add to \"signals\" > \"states\" > \"numeric\":");
                for (int i = 0; i < count; i++)
                {
                    uint smartObjId = startSmartObjectId + (uint)i;
                    sb.AppendLine($"\t\t\t\t\"{smartObjId}\": {{");
                    var entries = numericStates.Select(kvp => $"\t\t\t\t\t\"{kvp.Key}\": \"{componentName}[{i}].{kvp.Value}\"");
                    sb.AppendLine(string.Join(",\n", entries));
                    sb.AppendLine(i < count - 1 ? "\t\t\t\t}," : "\t\t\t\t}");
                }
                sb.AppendLine();
            }

            // String states
            if (stringStates != null && stringStates.Count > 0)
            {
                sb.AppendLine("// Add to \"signals\" > \"states\" > \"string\":");
                for (int i = 0; i < count; i++)
                {
                    uint smartObjId = startSmartObjectId + (uint)i;
                    sb.AppendLine($"\t\t\t\t\"{smartObjId}\": {{");
                    var entries = stringStates.Select(kvp => $"\t\t\t\t\t\"{kvp.Key}\": \"{componentName}[{i}].{kvp.Value}\"");
                    sb.AppendLine(string.Join(",\n", entries));
                    sb.AppendLine(i < count - 1 ? "\t\t\t\t}," : "\t\t\t\t}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Find the highest SmartObject ID used in the contract file
        /// </summary>
        public static uint GetNextSmartObjectId(string contractFilePath)
        {
            string content = File.ReadAllText(contractFilePath);
            
            // Find all numbers that are keys in the JSON (SmartObject IDs)
            // Pattern matches "###": { where ### is 1+ digits
            var matches = Regex.Matches(content, @"""(\d+)""\s*:\s*\{");
            
            uint maxId = 0;
            foreach (Match match in matches)
            {
                if (uint.TryParse(match.Groups[1].Value, out uint id))
                {
                    maxId = Math.Max(maxId, id);
                }
            }
            
            return maxId + 1;
        }

        #endregion

        #region C# Code Generation

        /// <summary>
        /// Generate a component .g.cs file
        /// </summary>
        public static string GenerateComponentCSharp(ComponentDefinition component)
        {
            var sb = new StringBuilder();
            string interfaceName = $"I{component.Name}";
            string delegateBool = $"{component.Name}BoolInputSigDelegate";
            string delegateUShort = $"{component.Name}UShortInputSigDelegate";
            string delegateString = $"{component.Name}StringInputSigDelegate";

            // Usings
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using Crestron.SimplSharpPro.DeviceSupport;");
            sb.AppendLine("using Crestron.SimplSharpPro;");
            sb.AppendLine();
            sb.AppendLine($"namespace Ch5_Sample_Contract.{component.Namespace}");
            sb.AppendLine("{");

            // Interface
            sb.AppendLine($"    public interface {interfaceName}");
            sb.AppendLine("    {");
            sb.AppendLine("        object UserObject { get; set; }");
            sb.AppendLine();

            // Events (from UI - outputs)
            foreach (var signal in component.BooleanOutputs)
            {
                sb.AppendLine($"        event EventHandler<UIEventArgs> {signal.Name};");
            }
            if (component.BooleanOutputs.Any()) sb.AppendLine();

            // Input methods (to UI)
            foreach (var signal in component.BooleanInputs)
            {
                sb.AppendLine($"        void {signal.Name}({delegateBool} callback);");
            }
            foreach (var signal in component.NumericInputs)
            {
                sb.AppendLine($"        void {signal.Name}({delegateUShort} callback);");
            }
            foreach (var signal in component.StringInputs)
            {
                sb.AppendLine($"        void {signal.Name}({delegateString} callback);");
            }

            sb.AppendLine();
            sb.AppendLine("    }");
            sb.AppendLine();

            // Delegates
            sb.AppendLine($"    public delegate void {delegateBool}(BoolInputSig boolInputSig, {interfaceName} {ToCamelCase(component.Name)});");
            if (component.NumericInputs.Any())
            {
                sb.AppendLine($"    public delegate void {delegateUShort}(UShortInputSig uShortInputSig, {interfaceName} {ToCamelCase(component.Name)});");
            }
            if (component.StringInputs.Any())
            {
                sb.AppendLine($"    public delegate void {delegateString}(StringInputSig stringInputSig, {interfaceName} {ToCamelCase(component.Name)});");
            }
            sb.AppendLine();

            // Class implementation
            sb.AppendLine($"    internal class {component.Name} : {interfaceName}, IDisposable");
            sb.AppendLine("    {");

            // Standard members region
            sb.AppendLine("        #region Standard CH5 Component members");
            sb.AppendLine();
            sb.AppendLine("        private ComponentMediator ComponentMediator { get; set; }");
            sb.AppendLine();
            sb.AppendLine("        public object UserObject { get; set; }");
            sb.AppendLine();
            sb.AppendLine("        public uint ControlJoinId { get; private set; }");
            sb.AppendLine();
            sb.AppendLine("        private IList<BasicTriListWithSmartObject> _devices;");
            sb.AppendLine("        public IList<BasicTriListWithSmartObject> Devices { get { return _devices; } }");
            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine();

            // Joins region
            sb.AppendLine("        #region Joins");
            sb.AppendLine();
            sb.AppendLine("        private static class Joins");
            sb.AppendLine("        {");
            
            // Boolean joins
            if (component.BooleanInputs.Any() || component.BooleanOutputs.Any())
            {
                sb.AppendLine("            internal static class Booleans");
                sb.AppendLine("            {");
                foreach (var signal in component.BooleanOutputs)
                {
                    sb.AppendLine($"                public const uint {signal.Name} = {signal.JoinNumber};");
                }
                if (component.BooleanOutputs.Any() && component.BooleanInputs.Any()) sb.AppendLine();
                foreach (var signal in component.BooleanInputs)
                {
                    sb.AppendLine($"                public const uint {signal.Name} = {signal.JoinNumber};");
                }
                sb.AppendLine("            }");
            }

            // Numeric joins
            if (component.NumericInputs.Any() || component.NumericOutputs.Any())
            {
                sb.AppendLine("            internal static class Numerics");
                sb.AppendLine("            {");
                foreach (var signal in component.NumericOutputs.Concat(component.NumericInputs))
                {
                    sb.AppendLine($"                public const uint {signal.Name} = {signal.JoinNumber};");
                }
                sb.AppendLine("            }");
            }

            // String joins
            if (component.StringInputs.Any() || component.StringOutputs.Any())
            {
                sb.AppendLine("            internal static class Strings");
                sb.AppendLine("            {");
                foreach (var signal in component.StringOutputs.Concat(component.StringInputs))
                {
                    sb.AppendLine($"                public const uint {signal.Name} = {signal.JoinNumber};");
                }
                sb.AppendLine("            }");
            }

            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine();

            // Construction and Initialization region
            sb.AppendLine("        #region Construction and Initialization");
            sb.AppendLine();
            sb.AppendLine($"        internal {component.Name}(ComponentMediator componentMediator, uint controlJoinId)");
            sb.AppendLine("        {");
            sb.AppendLine("            ComponentMediator = componentMediator;");
            sb.AppendLine("            Initialize(controlJoinId);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private void Initialize(uint controlJoinId)");
            sb.AppendLine("        {");
            sb.AppendLine("            ControlJoinId = controlJoinId;");
            sb.AppendLine();
            sb.AppendLine("            _devices = new List<BasicTriListWithSmartObject>();");
            sb.AppendLine();
            foreach (var signal in component.BooleanOutputs)
            {
                sb.AppendLine($"            ComponentMediator.ConfigureBooleanEvent(controlJoinId, Joins.Booleans.{signal.Name}, on{signal.Name});");
            }
            sb.AppendLine();
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void AddDevice(BasicTriListWithSmartObject device)");
            sb.AppendLine("        {");
            sb.AppendLine("            Devices.Add(device);");
            sb.AppendLine("            ComponentMediator.HookSmartObjectEvents(device.SmartObjects[ControlJoinId]);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void RemoveDevice(BasicTriListWithSmartObject device)");
            sb.AppendLine("        {");
            sb.AppendLine("            Devices.Remove(device);");
            sb.AppendLine("            ComponentMediator.UnHookSmartObjectEvents(device.SmartObjects[ControlJoinId]);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine();

            // CH5 Contract region
            sb.AppendLine("        #region CH5 Contract");
            sb.AppendLine();

            // Events (outputs from UI)
            foreach (var signal in component.BooleanOutputs)
            {
                sb.AppendLine($"        public event EventHandler<UIEventArgs> {signal.Name};");
                sb.AppendLine($"        private void on{signal.Name}(SmartObjectEventArgs eventArgs)");
                sb.AppendLine("        {");
                sb.AppendLine($"            EventHandler<UIEventArgs> handler = {signal.Name};");
                sb.AppendLine("            if (handler != null)");
                sb.AppendLine("                handler(this, UIEventArgs.CreateEventArgs(eventArgs));");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            // Boolean inputs (to UI)
            foreach (var signal in component.BooleanInputs)
            {
                sb.AppendLine($"        public void {signal.Name}({delegateBool} callback)");
                sb.AppendLine("        {");
                sb.AppendLine("            for (int index = 0; index < Devices.Count; index++)");
                sb.AppendLine("            {");
                sb.AppendLine($"                callback(Devices[index].SmartObjects[ControlJoinId].BooleanInput[Joins.Booleans.{signal.Name}], this);");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            // Numeric inputs (to UI)
            foreach (var signal in component.NumericInputs)
            {
                sb.AppendLine($"        public void {signal.Name}({delegateUShort} callback)");
                sb.AppendLine("        {");
                sb.AppendLine("            for (int index = 0; index < Devices.Count; index++)");
                sb.AppendLine("            {");
                sb.AppendLine($"                callback(Devices[index].SmartObjects[ControlJoinId].UShortInput[Joins.Numerics.{signal.Name}], this);");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            // String inputs (to UI)
            foreach (var signal in component.StringInputs)
            {
                sb.AppendLine($"        public void {signal.Name}({delegateString} callback)");
                sb.AppendLine("        {");
                sb.AppendLine("            for (int index = 0; index < Devices.Count; index++)");
                sb.AppendLine("            {");
                sb.AppendLine($"                callback(Devices[index].SmartObjects[ControlJoinId].StringInput[Joins.Strings.{signal.Name}], this);");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("        #endregion");
            sb.AppendLine();

            // Overrides region
            sb.AppendLine("        #region Overrides");
            sb.AppendLine();
            sb.AppendLine("        public override int GetHashCode()");
            sb.AppendLine("        {");
            sb.AppendLine("            return (int)ControlJoinId;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public override string ToString()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return string.Format(\"Contract: {{0}} Component: {{1}} HashCode: {{2}} {{3}}\", \"{component.Name}\", GetType().Name, GetHashCode(), UserObject != null ? \"UserObject: \" + UserObject : null);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine();

            // IDisposable region
            sb.AppendLine("        #region IDisposable");
            sb.AppendLine();
            sb.AppendLine("        public bool IsDisposed { get; set; }");
            sb.AppendLine();
            sb.AppendLine("        public void Dispose()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (IsDisposed)");
            sb.AppendLine("                return;");
            sb.AppendLine();
            sb.AppendLine("            IsDisposed = true;");
            sb.AppendLine();
            foreach (var signal in component.BooleanOutputs)
            {
                sb.AppendLine($"            {signal.Name} = null;");
            }
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        #endregion

        #region Generate Contract.g.cs Snippets

        /// <summary>
        /// Generate the SmartObjectIdMappings dictionary entry for Contract.g.cs
        /// </summary>
        public static string GenerateSmartObjectIdMappings(string componentName, uint startId, int count)
        {
            var sb = new StringBuilder();
            sb.Append($"        private static readonly IDictionary<int, uint> {componentName}SmartObjectIdMappings = new Dictionary<int, uint>{{");
            sb.AppendLine();
            sb.Append("            ");
            
            for (int i = 0; i < count; i++)
            {
                sb.Append($"{{ {i}, {startId + (uint)i} }}");
                if (i < count - 1)
                {
                    sb.Append(", ");
                    if ((i + 1) % 12 == 0) // Line break every 12 entries like original
                    {
                        sb.AppendLine();
                        sb.Append("            ");
                    }
                }
            }
            
            sb.AppendLine("};");
            return sb.ToString();
        }

        /// <summary>
        /// Generate the property declarations to add to Contract.g.cs
        /// </summary>
        public static string GenerateContractProperties(string componentName, string namespaceName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"        public Ch5_Sample_Contract.{namespaceName}.I{componentName}[] {componentName} {{ get {{ return Internal{componentName}.Cast<Ch5_Sample_Contract.{namespaceName}.I{componentName}>().ToArray(); }} }}");
            sb.AppendLine($"        private Ch5_Sample_Contract.{namespaceName}.{componentName}[] Internal{componentName} {{ get; set; }}");
            return sb.ToString();
        }

        /// <summary>
        /// Generate the constructor initialization code for Contract.g.cs
        /// </summary>
        public static string GenerateConstructorInit(string componentName, string namespaceName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"            Internal{componentName} = new Ch5_Sample_Contract.{namespaceName}.{componentName}[{componentName}SmartObjectIdMappings.Count];");
            sb.AppendLine($"            for (int index = 0; index < {componentName}SmartObjectIdMappings.Count; index++)");
            sb.AppendLine("            {");
            sb.AppendLine($"                Internal{componentName}[index] = new Ch5_Sample_Contract.{namespaceName}.{componentName}(ComponentMediator, {componentName}SmartObjectIdMappings[index]);");
            sb.AppendLine("            }");
            return sb.ToString();
        }

        /// <summary>
        /// Generate the AddDevice loop for Contract.g.cs
        /// </summary>
        public static string GenerateAddDeviceLoop(string componentName, int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"            for (int index = 0; index < {count}; index++)");
            sb.AppendLine("            {");
            sb.AppendLine($"                Internal{componentName}[index].AddDevice(device);");
            sb.AppendLine("            }");
            return sb.ToString();
        }

        /// <summary>
        /// Generate the RemoveDevice loop for Contract.g.cs
        /// </summary>
        public static string GenerateRemoveDeviceLoop(string componentName, int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"            for (int index = 0; index < {count}; index++)");
            sb.AppendLine("            {");
            sb.AppendLine($"                Internal{componentName}[index].RemoveDevice(device);");
            sb.AppendLine("            }");
            return sb.ToString();
        }

        /// <summary>
        /// Generate the Dispose loop for Contract.g.cs
        /// </summary>
        public static string GenerateDisposeLoop(string componentName, int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"            for (int index = 0; index < {count}; index++)");
            sb.AppendLine("            {");
            sb.AppendLine($"                Internal{componentName}[index].Dispose();");
            sb.AppendLine("            }");
            return sb.ToString();
        }

        /// <summary>
        /// Generate the ClearDictionaries line for Contract.g.cs
        /// </summary>
        public static string GenerateClearDictionariesLine(string componentName)
        {
            return $"            {componentName}SmartObjectIdMappings.Clear();";
        }

        /// <summary>
        /// Generate ALL the code snippets needed to add a component to Contract.g.cs
        /// </summary>
        public static string GenerateAllContractSnippets(string componentName, string namespaceName, uint startId, int count)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("// ============================================");
            sb.AppendLine($"// CODE TO ADD FOR: {componentName}");
            sb.AppendLine("// ============================================");
            sb.AppendLine();
            
            sb.AppendLine("// 1. Add to SmartObjectIdMappings section (around line 110):");
            sb.AppendLine(GenerateSmartObjectIdMappings(componentName, startId, count));
            sb.AppendLine();
            
            sb.AppendLine("// 2. Add to Properties section (around line 26):");
            sb.AppendLine(GenerateContractProperties(componentName, namespaceName));
            sb.AppendLine();
            
            sb.AppendLine("// 3. Add to Constructor initialization (around line 190):");
            sb.AppendLine(GenerateConstructorInit(componentName, namespaceName));
            sb.AppendLine();
            
            sb.AppendLine("// 4. Add to AddDevice() method:");
            sb.AppendLine(GenerateAddDeviceLoop(componentName, count));
            sb.AppendLine();
            
            sb.AppendLine("// 5. Add to RemoveDevice() method:");
            sb.AppendLine(GenerateRemoveDeviceLoop(componentName, count));
            sb.AppendLine();
            
            sb.AppendLine("// 6. Add to Dispose() method:");
            sb.AppendLine(GenerateDisposeLoop(componentName, count));
            sb.AppendLine();
            
            sb.AppendLine("// 7. Add to ClearDictionaries() method:");
            sb.AppendLine(GenerateClearDictionariesLine(componentName));
            
            return sb.ToString();
        }

        #endregion
    }
}

