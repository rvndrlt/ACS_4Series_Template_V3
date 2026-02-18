using System;
using System.Collections.Generic;
using System.IO;

namespace ACS_4Series_Template_V3.ContractGenerator
{
    /// <summary>
    /// Examples and helper methods for modifying CH5 contracts.
    /// Use these methods to add, modify, or remove components from your contract
    /// without needing the Crestron Contract Editor.
    /// </summary>
    public static class ContractExamples
    {
        // Path to your contract files - adjust as needed
        private static readonly string ContractJsonPath = @"ACS_Contract.txt";
        private static readonly string ContractCsPath = @"Contract";

        #region Example: Generate Simple Button Component

        /// <summary>
        /// Example: Generate all code needed for a simple button component
        /// Outputs the JSON and C# snippets to console/file
        /// </summary>
        public static void GenerateSimpleButtonComponent(string componentName, string namespaceName, uint startSmartObjectId, int count)
        {
            Console.WriteLine($"Generating {componentName} component with {count} instances starting at SmartObject ID {startSmartObjectId}");
            Console.WriteLine();

            // Define the signals
            var booleanStates = new Dictionary<uint, string>
            {
                { 1, "IsSelected" }
            };
            var booleanEvents = new Dictionary<uint, string>
            {
                { 1, "Press" }
            };
            var stringStates = new Dictionary<uint, string>
            {
                { 1, "Label" }
            };

            // Generate JSON snippet
            string jsonSnippet = ContractModifier.GenerateJsonSnippet(
                componentName,
                startSmartObjectId,
                count,
                booleanStates: booleanStates,
                booleanEvents: booleanEvents,
                stringStates: stringStates
            );
            Console.WriteLine(jsonSnippet);

            // Generate C# component class
            var component = new ContractModifier.ComponentDefinition
            {
                Name = componentName,
                Namespace = namespaceName,
                BooleanOutputs = new List<ContractModifier.SignalDefinition>
                {
                    new ContractModifier.SignalDefinition { Name = "Press", JoinNumber = 1 }
                },
                BooleanInputs = new List<ContractModifier.SignalDefinition>
                {
                    new ContractModifier.SignalDefinition { Name = "IsSelected", JoinNumber = 1 }
                },
                StringInputs = new List<ContractModifier.SignalDefinition>
                {
                    new ContractModifier.SignalDefinition { Name = "Label", JoinNumber = 1 }
                }
            };

            string componentCode = ContractModifier.GenerateComponentCSharp(component);
            
            // Save component file
            string dirPath = Path.Combine(ContractCsPath, namespaceName);
            Directory.CreateDirectory(dirPath);
            string filePath = Path.Combine(dirPath, $"{componentName}.g.cs");
            File.WriteAllText(filePath, componentCode);
            Console.WriteLine($"Component file written to: {filePath}");
            Console.WriteLine();

            // Generate Contract.g.cs snippets
            string contractSnippets = ContractModifier.GenerateAllContractSnippets(componentName, namespaceName, startSmartObjectId, count);
            Console.WriteLine(contractSnippets);
        }

        #endregion

        #region Example: Generate Music Zone Component

        /// <summary>
        /// Example: Generate all code needed for a music zone style component
        /// </summary>
        public static void GenerateMusicZoneComponent(string componentName, string namespaceName, uint startSmartObjectId, int count)
        {
            Console.WriteLine($"Generating {componentName} component with {count} instances starting at SmartObject ID {startSmartObjectId}");
            Console.WriteLine();

            // Define signals matching a typical music zone
            var booleanStates = new Dictionary<uint, string>
            {
                { 1, "ZoneSelected" },
                { 4, "ZoneMuted" },
                { 5, "ZoneOff" },
                { 6, "VolEnable" }
            };
            var booleanEvents = new Dictionary<uint, string>
            {
                { 1, "SelectZone" },
                { 2, "VolUp" },
                { 3, "VolDown" },
                { 4, "MuteZone" },
                { 5, "TurnZoneOff" }
            };
            var numericStates = new Dictionary<uint, string>
            {
                { 1, "Volume" }
            };
            var stringStates = new Dictionary<uint, string>
            {
                { 1, "ZoneName" },
                { 2, "ZoneSource" }
            };

            // Generate JSON snippet
            string jsonSnippet = ContractModifier.GenerateJsonSnippet(
                componentName,
                startSmartObjectId,
                count,
                booleanStates: booleanStates,
                booleanEvents: booleanEvents,
                numericStates: numericStates,
                stringStates: stringStates
            );
            Console.WriteLine(jsonSnippet);

            // Generate C# component
            var component = new ContractModifier.ComponentDefinition
            {
                Name = componentName,
                Namespace = namespaceName,
                BooleanOutputs = new List<ContractModifier.SignalDefinition>
                {
                    new ContractModifier.SignalDefinition { Name = "SelectZone", JoinNumber = 1 },
                    new ContractModifier.SignalDefinition { Name = "VolUp", JoinNumber = 2 },
                    new ContractModifier.SignalDefinition { Name = "VolDown", JoinNumber = 3 },
                    new ContractModifier.SignalDefinition { Name = "MuteZone", JoinNumber = 4 },
                    new ContractModifier.SignalDefinition { Name = "TurnZoneOff", JoinNumber = 5 }
                },
                BooleanInputs = new List<ContractModifier.SignalDefinition>
                {
                    new ContractModifier.SignalDefinition { Name = "ZoneSelected", JoinNumber = 1 },
                    new ContractModifier.SignalDefinition { Name = "ZoneMuted", JoinNumber = 4 },
                    new ContractModifier.SignalDefinition { Name = "ZoneOff", JoinNumber = 5 },
                    new ContractModifier.SignalDefinition { Name = "VolEnable", JoinNumber = 6 }
                },
                NumericInputs = new List<ContractModifier.SignalDefinition>
                {
                    new ContractModifier.SignalDefinition { Name = "Volume", JoinNumber = 1 }
                },
                StringInputs = new List<ContractModifier.SignalDefinition>
                {
                    new ContractModifier.SignalDefinition { Name = "ZoneName", JoinNumber = 1 },
                    new ContractModifier.SignalDefinition { Name = "ZoneSource", JoinNumber = 2 }
                }
            };

            string componentCode = ContractModifier.GenerateComponentCSharp(component);
            
            string dirPath = Path.Combine(ContractCsPath, namespaceName);
            Directory.CreateDirectory(dirPath);
            string filePath = Path.Combine(dirPath, $"{componentName}.g.cs");
            File.WriteAllText(filePath, componentCode);
            Console.WriteLine($"Component file written to: {filePath}");
            Console.WriteLine();

            string contractSnippets = ContractModifier.GenerateAllContractSnippets(componentName, namespaceName, startSmartObjectId, count);
            Console.WriteLine(contractSnippets);
        }

        #endregion

        #region Example: Get Contract Info

        /// <summary>
        /// Get information about the current contract
        /// </summary>
        public static void PrintContractInfo()
        {
            if (!File.Exists(ContractJsonPath))
            {
                Console.WriteLine($"Contract file not found: {ContractJsonPath}");
                return;
            }

            uint nextId = ContractModifier.GetNextSmartObjectId(ContractJsonPath);
            Console.WriteLine($"Contract file: {ContractJsonPath}");
            Console.WriteLine($"Next available SmartObject ID: {nextId}");
        }

        #endregion

        #region Helper: Quick Reference

        /// <summary>
        /// Print a quick reference for manual modifications
        /// </summary>
        public static void PrintQuickReference()
        {
            Console.WriteLine(@"
=== CH5 Contract Quick Reference ===

SIGNAL TYPES IN JSON:
- signals.states.boolean  = Boolean feedback TO UI (e.g., IsSelected)
- signals.states.numeric  = Numeric feedback TO UI (e.g., Volume 0-65535)
- signals.states.string   = String feedback TO UI (e.g., Label text)
- signals.events.boolean  = Boolean input FROM UI (e.g., button press)
- signals.events.numeric  = Numeric input FROM UI (e.g., slider change)
- signals.events.string   = String input FROM UI (e.g., text entry)

FILE STRUCTURE:
- ACS_Contract.txt           = JSON contract definition
- Contract/Contract.g.cs     = Main contract class
- Contract/[Namespace]/      = Component folders
  └── [Component].g.cs       = Component class

ADDING A NEW COMPONENT:
1. Add signals to ACS_Contract.txt in appropriate sections
2. Create Contract/[Namespace]/[Component].g.cs
3. Update Contract.g.cs:
   - Add SmartObjectIdMappings dictionary
   - Add interface/class properties
   - Add initialization in constructor
   - Add to AddDevice() method
   - Add to RemoveDevice() method
   - Add to Dispose() method
   - Add to ClearDictionaries() method
");
        }

        #endregion
    }
}
