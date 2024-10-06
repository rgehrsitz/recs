// RECS.Compiler/BytecodeGenerator.cs

using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using RECS.Core;
using Serilog;

namespace RECS.Compiler
{
    public enum Opcode
    {
        Nop = 0,
        JumpIfFalse = 1,
        JumpIfTrue = 2,
        ExecuteAction = 3,
        RuleStart = 4,
        RuleEnd = 5,
        Priority = 6,
        LoadFactFloat = 7,
        LoadFactString = 8,
        LoadFactBool = 9,
        LoadConstFloat = 10,
        LoadConstString = 11,
        LoadConstBool = 12,
        GtFloat = 13,
        EqFloat = 14,
        EqString = 15,
        EqBool = 16,
        NeqFloat = 17,
        NeqString = 18,
        NeqBool = 19,
        LtFloat = 20,
        LteFloat = 21,
        GteFloat = 22,
        ContainsString = 23,
        NotContainsString = 24,
        ActionStart = 25,
        ActionType = 26,
        ActionTarget = 27,
        ActionValueFloat = 28,
        ActionValueString = 29,
        ActionValueBool = 30,
        ScriptCall = 31,
        ScriptDef = 32,
        Label = 33,
    }

    public class Instruction
    {
        public Opcode Opcode { get; set; }
        public List<byte> Operands { get; set; } = new List<byte>();

        public int Size => 1 + Operands.Count; // 1 byte for opcode + size of operands
    }

    public class BytecodeGenerator
    {
        private int _labelCounter;
        private Dictionary<string, int> _labelPositions = new Dictionary<string, int>();
        public Dictionary<string, int> PendingLabelReplacements { get; private set; } = 
            new Dictionary<string, int>();

        // Pools for label management
        private List<string> _availableLabels = new List<string>();
        private Dictionary<string, bool> _usedLabels = new Dictionary<string, bool>();

        // Generate a new label or reuse one from the pool
        private string GetNextLabel(string prefix)
        {
            if (_availableLabels.Count > 0)
            {
                var label = _availableLabels[0];
                _availableLabels.RemoveAt(0);
                Log.Debug($"Reusing label: {label}");
                return label;
            }
            else
            {
                var label = $"{prefix}{_labelCounter:D3}";
                _labelCounter++;
                Log.Debug($"Generated new label: {label}");
                return label;
            }
        }

        // Release a label back to the pool
        private void ReleaseLabel(string label)
        {
            if (!_usedLabels.ContainsKey(label))
                return;
            _usedLabels.Remove(label);
            _availableLabels.Add(label);
            Log.Debug($"Released label: {label}");
        }

        public List<Instruction> Generate(Ruleset ruleset)
        {
            var instructions = new List<Instruction>();

            foreach (var rule in ruleset.Rules)
            {
                Log.Information($"Generating bytecode for rule: {rule.Name}");
                instructions.Add(new Instruction { Opcode = Opcode.RuleStart });
                
                // Encode rule name and priority
                instructions.AddRange(EncodeString(rule.Name));
                instructions.Add(new Instruction { Opcode = Opcode.Priority });
                instructions.AddRange(EncodeInteger(rule.Priority));

                // Generate conditions and actions
                var conditionNode = ConvertConditionGroupToNode(rule.Conditions);
                var successLabel = GetNextLabel("L");
                var failLabel = GetNextLabel("L");
                instructions.AddRange(GenerateInstructionsForConditions(conditionNode, successLabel, failLabel));

                // Mark success and fail labels
                instructions.Add(new Instruction { Opcode = Opcode.Label, Operands = new List<byte>(Encoding.UTF8.GetBytes(successLabel)) });
                instructions.Add(new Instruction { Opcode = Opcode.Label, Operands = new List<byte>(Encoding.UTF8.GetBytes(failLabel)) });

                foreach (var action in rule.Actions)
                {
                    Log.Information($"Adding action: {action.Type} to target {action.Target}");
                    instructions.Add(GenerateActionInstruction(action));
                }

                // Add script handling if defined
                if (rule.Scripts != null)
                {
                    foreach (var script in rule.Scripts)
                    {
                        Log.Information($"Adding script: {script.Key} to rule: {rule.Name}");
                        instructions.Add(GenerateScriptInstruction(script.Value, script.Key));
                    }
                }

                instructions.Add(new Instruction { Opcode = Opcode.RuleEnd });
            }

            // Replace any pending labels with actual offsets
            ReplaceLabels(instructions);

            return instructions;
        }

        public void ReplaceLabels(List<Instruction> instructions)
        {
            Log.Debug("Replacing labels with actual offsets...");

            // Step 1: Record all label positions
            for (int i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                if (instruction.Opcode == Opcode.Label)
                {
                    var label = Encoding.UTF8.GetString(instruction.Operands.ToArray());
                    _labelPositions[label] = i;
                }
            }

            // Step 2: Replace pending labels with the correct offsets
            foreach (var entry in PendingLabelReplacements)
            {
                var label = entry.Key;
                var instructionIndex = entry.Value;

                if (_labelPositions.TryGetValue(label, out var labelPosition))
                {
                    var offset = labelPosition - instructionIndex - 1;
                    var offsetBytes = BitConverter.GetBytes(offset);
                    instructions[instructionIndex].Operands.Clear();
                    instructions[instructionIndex].Operands.AddRange(offsetBytes);
                }
                else
                {
                    throw new InvalidOperationException($"Label {label} not found for replacement.");
                }
            }
        }

        public Instruction GenerateConditionInstruction(ConditionOrGroup condition, bool isAnyCondition)
        {
            var instruction = new Instruction
            {
                Opcode = isAnyCondition ? Opcode.JumpIfTrue : Opcode.JumpIfFalse,
            };

            // Infer fact type
            var factType = condition.Value.GetType(); // Default to string if null

            // Encode the fact
            instruction.Operands.AddRange(EncodeString(condition.Fact)[1].Operands);

            // Map operator and encode the value based on its type
            instruction.Opcode = MapOperatorToOpcode(condition.Operator, factType);
            instruction.Operands.AddRange(EncodeValue(condition.Value));

            return instruction;
        }

        private static Opcode MapOperatorToOpcode(string operatorStr, Type factType)
        {
            if (factType == typeof(float) || factType == typeof(int))
            {
                return operatorStr switch
                {
                    "GT" => Opcode.GtFloat,
                    "EQ" => Opcode.EqFloat,
                    "NEQ" => Opcode.NeqFloat,
                    "LT" => Opcode.LtFloat,
                    "GTE" => Opcode.GteFloat,
                    "LTE" => Opcode.LteFloat,
                    _ => throw new InvalidOperationException($"Unsupported operator: {operatorStr} for float/int type"),
                };
            }
            else if (factType == typeof(string))
            {
                return operatorStr switch
                {
                    "EQ" => Opcode.EqString,
                    "NEQ" => Opcode.NeqString,
                    "CONTAINS" => Opcode.ContainsString,
                    "NOT_CONTAINS" => Opcode.NotContainsString,
                    _ => throw new InvalidOperationException($"Unsupported operator: {operatorStr} for string type"),
                };
            }
            else if (factType == typeof(bool))
            {
                return operatorStr switch
                {
                    "EQ" => Opcode.EqBool,
                    "NEQ" => Opcode.NeqBool,
                    _ => throw new InvalidOperationException($"Unsupported operator: {operatorStr} for bool type"),
                };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported fact type: {factType}");
            }
        }

        private static List<byte> EncodeValue(object value)
        {
            switch (value)
            {
                case int intValue:
                    return [..BitConverter.GetBytes(intValue)];
                case float floatValue:
                    return [..BitConverter.GetBytes(floatValue)];
                case string strValue:
                {
                    var stringBytes = Encoding.UTF8.GetBytes(strValue);
                    var lengthBytes = BitConverter.GetBytes((ushort)stringBytes.Length);
                    return [..lengthBytes.Concat(stringBytes).ToList()];
                }
                case bool boolValue:
                    return [(byte)(boolValue ? 1 : 0)];
                default:
                    throw new InvalidOperationException($"Unsupported value type: {value?.GetType().Name}");
            }
        }

        private Instruction GenerateActionInstruction(RuleAction action)
        {
            var instruction = new Instruction { Opcode = Opcode.ExecuteAction };
            instruction.Operands.AddRange(EncodeString(action.Type)[1].Operands);
            instruction.Operands.AddRange(EncodeString(action.Target)[1].Operands);
            instruction.Operands.AddRange(EncodeValue(action.Value));
            return instruction;
        }

        private Instruction GenerateScriptInstruction(UserScript script, string scriptName)
        {
            var instruction = new Instruction { Opcode = Opcode.ScriptDef };

            // Add script name
            instruction.Operands.AddRange(EncodeString(scriptName)[1].Operands);

            // Compile script using Roslyn
            try
            {
                var compiledScript = CSharpScript.Create(script.Body, ScriptOptions.Default);
                compiledScript.Compile(); // Ensure script is valid

                Log.Information($"Compiled script: {scriptName} successfully.");

                // Add compiled script indication (this is just a placeholder for execution)
                instruction.Operands.AddRange(Encoding.UTF8.GetBytes("CompiledScript"));
            }
            catch (CompilationErrorException e)
            {
                Log.Error($"Script compilation failed for {scriptName}: {string.Join(", ", e.Diagnostics)}");
                throw new InvalidOperationException($"Script compilation failed: {string.Join(", ", e.Diagnostics)}");
            }

            // Add script parameters (if any)
            instruction.Operands.AddRange(BitConverter.GetBytes(script.Params?.Count ?? 0));
            if (script.Params == null) return instruction;
            foreach (var param in script.Params)
            {
                instruction.Operands.AddRange(EncodeString(param)[1].Operands);
            }

            return instruction;
        }

        // Method to execute a script using Roslyn during runtime
        public async Task<object> ExecuteScriptAsync(string scriptBody, Dictionary<string, object> parameters)
        {
            try
            {
                var scriptOptions = ScriptOptions.Default.AddReferences(typeof(object).Assembly);
                var result = await CSharpScript.EvaluateAsync(scriptBody, scriptOptions, globals: new Globals(parameters));
                return result;
            }
            catch (CompilationErrorException e)
            {
                throw new InvalidOperationException($"Script execution failed: {string.Join(", ", e.Diagnostics)}");
            }
        }

        // Class to hold global parameters for the script
        private class Globals(Dictionary<string, object> parameters)
        {
            public Dictionary<string, object> Parameters { get; } = parameters;
        }

        // Utility method to encode strings into bytecode
        public List<Instruction> EncodeString(string str)
        {
            var instructions = new List<Instruction>();
            var stringBytes = Encoding.UTF8.GetBytes(str);
            var lengthBytes = BitConverter.GetBytes((ushort)stringBytes.Length);

            instructions.Add(new Instruction
            {
                Opcode = Opcode.LoadConstString,
                Operands = [..lengthBytes],
            });
            instructions.Add(new Instruction
            {
                Opcode = Opcode.LoadConstString,
                Operands = [..stringBytes],
            });

            return instructions;
        }

        public static List<Instruction> EncodeInteger(int value)
        {
            var instructions = new List<Instruction>();
            var intBytes = BitConverter.GetBytes(value);

            instructions.Add(new Instruction
            {
                Opcode = Opcode.LoadConstFloat,
                Operands = new List<byte>(intBytes),
            });

            return instructions;
        }

        private static ConditionOrGroup ConvertConditionGroupToNode(ConditionGroup conditions)
        {
            var rootNode = new ConditionOrGroup();

            if (conditions.All != null && conditions.All.Count > 0)
            {
                rootNode.All = conditions.All;
            }

            if (conditions.Any != null && conditions.Any.Count > 0)
            {
                rootNode.Any = conditions.Any;
            }

            return rootNode;
        }

        private List<Instruction> GenerateInstructionsForConditions(ConditionOrGroup conditionNode, string successLabel, string failLabel)
        {
            var instructions = new List<Instruction>();

            // Traverse the 'all' conditions (AND logic)
            if (conditionNode.All != null && conditionNode.All.Count > 0)
            {
                string nextFailLabel = failLabel;
                for (int i = 0; i < conditionNode.All.Count; i++)
                {
                    var nextSuccessLabel = i == conditionNode.All.Count - 1 ? successLabel : GetNextLabel("L");
                    instructions.AddRange(GenerateInstructionsForConditions(conditionNode.All[i], nextSuccessLabel, nextFailLabel));
                    if (i != conditionNode.All.Count - 1)
                    {
                        instructions.Add(new Instruction { Opcode = Opcode.Label, Operands = new List<byte>(Encoding.UTF8.GetBytes(nextSuccessLabel)) });
                    }
                }
            }
            // Traverse the 'any' conditions (OR logic)
            else if (conditionNode.Any != null && conditionNode.Any.Count > 0)
            {
                for (int i = 0; i < conditionNode.Any.Count; i++)
                {
                    var nextFailLabel = i == conditionNode.Any.Count - 1 ? failLabel : GetNextLabel("L");
                    instructions.AddRange(GenerateInstructionsForConditions(conditionNode.Any[i], successLabel, nextFailLabel));
                    if (i != conditionNode.Any.Count - 1)
                    {
                        instructions.Add(new Instruction { Opcode = Opcode.Label, Operands = new List<byte>(Encoding.UTF8.GetBytes(nextFailLabel)) });
                    }
                }
            }
            // Base case: single condition (leaf node)
            else
            {
                // Generate condition instruction with jump to failLabel if false, successLabel if true
                instructions.Add(GenerateConditionInstruction(conditionNode, false)); // JUMP_IF_FALSE to failLabel
                instructions.Add(new Instruction 
                { 
                    Opcode = Opcode.JumpIfTrue,
                    Operands = new List<byte>(Encoding.UTF8.GetBytes(successLabel)),
                });
            }

            return instructions;
        }

        private static List<string> CollectFacts(Instruction instruction)
        {
            var facts = new List<string>();

            // Check if the instruction deals with facts
            if (instruction.Opcode == Opcode.LoadFactFloat || 
                instruction.Opcode == Opcode.LoadFactString || 
                instruction.Opcode == Opcode.LoadFactBool)
            {
                var fact = Encoding.UTF8.GetString(instruction.Operands.ToArray());
                facts.Add(fact);
            }

            return facts;
        }

        private void UpdateDependencyIndexes(List<Instruction> instructions)
        {
            var factDependencyIndex = new Dictionary<string, List<string>>();

            foreach (var instruction in instructions)
            {
                var facts = CollectFacts(instruction);
                foreach (var fact in facts)
                {
                    if (!factDependencyIndex.ContainsKey(fact))
                    {
                        factDependencyIndex[fact] = [];
                    }
                    // Add dependencies
                    factDependencyIndex[fact].Add("SomeRuleName");
                }
            }
        }

        private List<Instruction> OptimizeInstructions(List<Instruction> instructions)
        {
            var optimizedInstructions = new List<Instruction>();

            for (var i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];

                // Combine JUMP_IF_TRUE and JUMP_IF_FALSE if they are adjacent
                if (instruction.Opcode == Opcode.JumpIfFalse && 
                    i + 1 < instructions.Count && 
                    instructions[i + 1].Opcode == Opcode.JumpIfTrue)
                {
                    optimizedInstructions.Add(new Instruction { Opcode = Opcode.Nop });
                    i++; // Skip the next instruction (JUMP_IF_TRUE)
                }
                else
                {
                    optimizedInstructions.Add(instruction);
                }
            }

            // Remove unused labels
            return optimizedInstructions.FindAll(instr => instr.Opcode != Opcode.Label);
        }

        // UserScript call handling during condition or bytecode execution phase
        private Instruction GenerateScriptCallInstruction(string scriptName, List<string>? parameters)
        {
            var instruction = new Instruction { Opcode = Opcode.ScriptCall };

            // Add script name
            instruction.Operands.AddRange(EncodeString(scriptName)[1].Operands);

            // Add parameters
            instruction.Operands.AddRange(BitConverter.GetBytes(parameters?.Count ?? 0));
            if (parameters == null) return instruction;
            foreach (var param in parameters)
            {
                instruction.Operands.AddRange(EncodeString(param)[1].Operands);
            }

            return instruction;
        }
    }
}