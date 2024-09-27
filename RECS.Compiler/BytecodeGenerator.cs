// RECS.Compiler/BytecodeGenerator.cs

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using RECS.Core;

namespace RECS.Compiler
{
    public enum Opcode
    {
        NOP = 0,
        JUMP_IF_FALSE = 1,
        JUMP_IF_TRUE = 2,
        EXECUTE_ACTION = 3,
        RULE_START = 4,
        RULE_END = 5,
        PRIORITY = 6,
        LOAD_FACT_FLOAT = 7,
        LOAD_FACT_STRING = 8,
        LOAD_FACT_BOOL = 9,
        LOAD_CONST_FLOAT = 10,
        LOAD_CONST_STRING = 11,
        LOAD_CONST_BOOL = 12,
        GT_FLOAT = 13,
        EQ_FLOAT = 14,
        EQ_STRING = 15,
        EQ_BOOL = 16,
        NEQ_FLOAT = 17,
        NEQ_STRING = 18,
        NEQ_BOOL = 19,
        LT_FLOAT = 20,
        LTE_FLOAT = 21,
        GTE_FLOAT = 22,
        CONTAINS_STRING = 23,
        NOT_CONTAINS_STRING = 24,
        ACTION_START = 25,
        ACTION_TYPE = 26,
        ACTION_TARGET = 27,
        ACTION_VALUE_FLOAT = 28,
        ACTION_VALUE_STRING = 29,
        ACTION_VALUE_BOOL = 30,
        SCRIPT_CALL = 31,
        SCRIPT_DEF = 32,
        LABEL = 33,
    }

    public class Instruction
    {
        public Opcode Opcode { get; set; }
        public List<byte> Operands { get; set; } = new List<byte>();

        public int Size => 1 + Operands.Count; // 1 byte for opcode + size of operands
    }

    public class BytecodeGenerator
    {
        private int labelCounter = 0;
        private Dictionary<string, int> labelPositions = new Dictionary<string, int>();
        public Dictionary<string, int> PendingLabelReplacements { get; private set; } =
            new Dictionary<string, int>();

        // Pools for label management
        private List<string> availableLabels = new List<string>();
        private Dictionary<string, bool> usedLabels = new Dictionary<string, bool>();

        // Generate a new label or reuse one from the pool
        private string getNextLabel(string prefix)
        {
            if (availableLabels.Count > 0)
            {
                var label = availableLabels[0];
                availableLabels.RemoveAt(0);
                return label;
            }
            else
            {
                var label = $"{prefix}{labelCounter:D3}";
                labelCounter++;
                return label;
            }
        }

        // Release a label back to the pool
        private void releaseLabel(string label)
        {
            if (!usedLabels.ContainsKey(label))
                return;
            usedLabels.Remove(label);
            availableLabels.Add(label);
        }

        public List<Instruction> Generate(Ruleset ruleset)
        {
            var instructions = new List<Instruction>();

            foreach (var rule in ruleset.Rules)
            {
                instructions.Add(new Instruction { Opcode = Opcode.RULE_START });

                // Encode rule name and priority
                instructions.AddRange(EncodeString(rule.Name ?? string.Empty));
                instructions.Add(new Instruction { Opcode = Opcode.PRIORITY });
                instructions.AddRange(EncodeInteger(rule.Priority));

                // Generate conditions and actions
                var conditionNode = ConvertConditionGroupToNode(rule.Conditions);
                var successLabel = getNextLabel("L");
                var failLabel = getNextLabel("L");
                instructions.AddRange(
                    GenerateInstructionsForConditions(conditionNode, successLabel, failLabel)
                );

                // Mark success and fail labels
                instructions.Add(
                    new Instruction
                    {
                        Opcode = Opcode.LABEL,
                        Operands = new List<byte>(Encoding.UTF8.GetBytes(successLabel)),
                    }
                );
                instructions.Add(
                    new Instruction
                    {
                        Opcode = Opcode.LABEL,
                        Operands = new List<byte>(Encoding.UTF8.GetBytes(failLabel)),
                    }
                );

                foreach (var action in rule.Actions)
                {
                    instructions.Add(GenerateActionInstruction(action));
                }

                instructions.Add(new Instruction { Opcode = Opcode.RULE_END });
            }

            // Replace any pending labels with actual offsets
            ReplaceLabels(instructions);

            return instructions;
        }

        private List<Instruction> GenerateInstructionsForConditions(
            ConditionOrGroup conditionNode,
            string successLabel,
            string failLabel
        )
        {
            var instructions = new List<Instruction>();

            // Traverse the 'all' conditions (AND logic)
            if (conditionNode.All != null && conditionNode.All.Count > 0)
            {
                string nextFailLabel = failLabel;
                for (int i = 0; i < conditionNode.All.Count; i++)
                {
                    var nextSuccessLabel =
                        i == conditionNode.All.Count - 1 ? successLabel : getNextLabel("L");
                    instructions.AddRange(
                        GenerateInstructionsForConditions(
                            conditionNode.All[i],
                            nextSuccessLabel,
                            nextFailLabel
                        )
                    );
                    if (i != conditionNode.All.Count - 1)
                    {
                        instructions.Add(
                            new Instruction
                            {
                                Opcode = Opcode.LABEL,
                                Operands = new List<byte>(Encoding.UTF8.GetBytes(nextSuccessLabel)),
                            }
                        );
                    }
                }
            }
            // Traverse the 'any' conditions (OR logic)
            else if (conditionNode.Any != null && conditionNode.Any.Count > 0)
            {
                for (int i = 0; i < conditionNode.Any.Count; i++)
                {
                    var nextFailLabel =
                        i == conditionNode.Any.Count - 1 ? failLabel : getNextLabel("L");
                    instructions.AddRange(
                        GenerateInstructionsForConditions(
                            conditionNode.Any[i],
                            successLabel,
                            nextFailLabel
                        )
                    );
                    if (i != conditionNode.Any.Count - 1)
                    {
                        instructions.Add(
                            new Instruction
                            {
                                Opcode = Opcode.LABEL,
                                Operands = new List<byte>(Encoding.UTF8.GetBytes(nextFailLabel)),
                            }
                        );
                    }
                }
            }
            // Base case: single condition (leaf node)
            else if (conditionNode.Fact != null)
            {
                // Generate condition instruction with jump to failLabel if false, successLabel if true
                instructions.Add(GenerateConditionInstruction(conditionNode, false)); // JUMP_IF_FALSE to failLabel
                instructions.Add(
                    new Instruction
                    {
                        Opcode = Opcode.JUMP_IF_TRUE,
                        Operands = new List<byte>(Encoding.UTF8.GetBytes(successLabel)),
                    }
                );
            }

            return instructions;
        }

        public void ReplaceLabels(List<Instruction> instructions)
        {
            // Step 1: Record all label positions
            foreach (var instruction in instructions)
            {
                if (instruction.Opcode == Opcode.LABEL)
                {
                    string label = Encoding.UTF8.GetString(instruction.Operands.ToArray());
                    if (label.Length == 4) // Ensure label is 4 bytes
                    {
                        labelPositions[label] = instructions.IndexOf(instruction);
                    }
                }
            }

            // Step 2: Replace pending labels with the correct offsets
            foreach (var entry in PendingLabelReplacements)
            {
                string label = entry.Key;
                int instructionIndex = entry.Value;

                // Check if label position exists
                if (labelPositions.TryGetValue(label, out int labelPosition))
                {
                    // Calculate relative offset from the jump instruction to the label
                    int offset = labelPosition - instructionIndex - 1; // Relative to next instruction

                    // Convert offset to 4-byte (int) and replace the label
                    byte[] offsetBytes = BitConverter.GetBytes(offset);

                    // Ensure we replace exactly 4 bytes in the operands (this assumes the operands already reserve 4 bytes for the label)
                    instructions[instructionIndex].Operands.RemoveRange(0, 4); // Remove 4-byte label
                    instructions[instructionIndex].Operands.InsertRange(0, offsetBytes); // Insert offset bytes
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Label {label} not found for replacement."
                    );
                }
            }
        }

        public Instruction GenerateConditionInstruction(
            ConditionOrGroup condition,
            bool isAnyCondition
        )
        {
            var instruction = new Instruction
            {
                Opcode = isAnyCondition ? Opcode.JUMP_IF_TRUE : Opcode.JUMP_IF_FALSE,
            };

            // Infer fact type
            Type factType = condition.Value?.GetType() ?? typeof(string); // Default to string if null

            // Encode the fact
            var factBytes = Encoding.UTF8.GetBytes(condition.Fact ?? string.Empty);
            instruction.Operands.AddRange(EncodeString(condition.Fact ?? string.Empty)[1].Operands);

            // Map operator and encode the value based on its type
            instruction.Opcode = MapOperatorToOpcode(condition.Operator, factType);
            if (condition.Value != null)
            {
                instruction.Operands.AddRange(EncodeValue(condition.Value));
            }

            return instruction;
        }

        private Opcode MapOperatorToOpcode(string operatorStr, Type factType)
        {
            if (factType == typeof(float) || factType == typeof(int))
            {
                return operatorStr switch
                {
                    "GT" => Opcode.GT_FLOAT,
                    "EQ" => Opcode.EQ_FLOAT,
                    "NEQ" => Opcode.NEQ_FLOAT,
                    "LT" => Opcode.LT_FLOAT,
                    "GTE" => Opcode.GTE_FLOAT,
                    "LTE" => Opcode.LTE_FLOAT,
                    _ => throw new InvalidOperationException(
                        $"Unsupported operator: {operatorStr} for float/int type"
                    ),
                };
            }
            else if (factType == typeof(string))
            {
                return operatorStr switch
                {
                    "EQ" => Opcode.EQ_STRING,
                    "NEQ" => Opcode.NEQ_STRING,
                    "CONTAINS" => Opcode.CONTAINS_STRING,
                    "NOT_CONTAINS" => Opcode.NOT_CONTAINS_STRING,
                    _ => throw new InvalidOperationException(
                        $"Unsupported operator: {operatorStr} for string type"
                    ),
                };
            }
            else if (factType == typeof(bool))
            {
                return operatorStr switch
                {
                    "EQ" => Opcode.EQ_BOOL,
                    "NEQ" => Opcode.NEQ_BOOL,
                    _ => throw new InvalidOperationException(
                        $"Unsupported operator: {operatorStr} for bool type"
                    ),
                };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported fact type: {factType}");
            }
        }

        private List<byte> EncodeValue(object value)
        {
            if (value is int intValue)
            {
                return new List<byte>(BitConverter.GetBytes(intValue));
            }
            if (value is float floatValue)
            {
                return new List<byte>(BitConverter.GetBytes(floatValue));
            }
            if (value is string strValue)
            {
                var stringBytes = Encoding.UTF8.GetBytes(strValue);
                var lengthBytes = BitConverter.GetBytes((ushort)stringBytes.Length);
                return new List<byte>(lengthBytes.Concat(stringBytes).ToList());
            }
            if (value is bool boolValue)
            {
                return new List<byte> { (byte)(boolValue ? 1 : 0) };
            }

            throw new InvalidOperationException($"Unsupported value type: {value?.GetType().Name}");
        }

        private Instruction GenerateActionInstruction(RuleAction action)
        {
            var instruction = new Instruction { Opcode = Opcode.EXECUTE_ACTION };

            // Handle possible null values
            var typeBytes = Encoding.UTF8.GetBytes(action.Type ?? string.Empty);
            var targetBytes = Encoding.UTF8.GetBytes(action.Target ?? string.Empty);
            var valueBytes = Encoding.UTF8.GetBytes(action.Value?.ToString() ?? string.Empty);

            instruction.Operands.AddRange(typeBytes);
            instruction.Operands.AddRange(targetBytes);
            instruction.Operands.AddRange(valueBytes);

            return instruction;
        }

        private Instruction GenerateScriptInstruction(Script script, string scriptName)
        {
            var instruction = new Instruction { Opcode = Opcode.SCRIPT_DEF };

            // Add script name
            var scriptNameBytes = Encoding.UTF8.GetBytes(scriptName);
            instruction.Operands.AddRange(scriptNameBytes);

            // Add script parameters
            var paramCount = script.Params?.Count ?? 0; // Handle null `Params` by using 0
            var paramCountBytes = BitConverter.GetBytes(paramCount);
            instruction.Operands.AddRange(paramCountBytes);

            if (script.Params != null)
            {
                foreach (var param in script.Params)
                {
                    var paramBytes = Encoding.UTF8.GetBytes(param);
                    instruction.Operands.AddRange(paramBytes);
                }
            }

            // Add script body
            var bodyBytes = Encoding.UTF8.GetBytes(script.Body);
            instruction.Operands.AddRange(bodyBytes);

            return instruction;
        }

        // Utility method to encode strings into bytecode
        public List<Instruction> EncodeString(string str)
        {
            var instructions = new List<Instruction>();
            var stringBytes = Encoding.UTF8.GetBytes(str);
            var lengthBytes = BitConverter.GetBytes((ushort)stringBytes.Length); // Ensure 2-byte length

            // Create an instruction for the length and another for the string data
            instructions.Add(
                new Instruction
                {
                    Opcode = Opcode.LOAD_CONST_STRING,
                    Operands = new List<byte>(lengthBytes),
                }
            );
            instructions.Add(
                new Instruction
                {
                    Opcode = Opcode.LOAD_CONST_STRING,
                    Operands = new List<byte>(stringBytes),
                }
            );

            return instructions;
        }

        public List<Instruction> EncodeInteger(int value)
        {
            var instructions = new List<Instruction>();
            var intBytes = BitConverter.GetBytes(value);

            // Fix here: Convert byte[] to List<byte>
            instructions.Add(
                new Instruction
                {
                    Opcode = Opcode.LOAD_CONST_FLOAT, // or appropriate opcode
                    Operands = new List<byte>(intBytes),
                }
            );

            return instructions;
        }

        private ConditionOrGroup ConvertConditionGroupToNode(ConditionGroup conditions)
        {
            var rootNode = new ConditionOrGroup();

            // Combine 'all' and 'any' conditions into a single root node
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

        private List<string> CollectFacts(Instruction instruction)
        {
            var facts = new List<string>();

            // Check if the instruction deals with facts
            if (
                instruction.Opcode == Opcode.LOAD_FACT_FLOAT
                || instruction.Opcode == Opcode.LOAD_FACT_STRING
                || instruction.Opcode == Opcode.LOAD_FACT_BOOL
            )
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
                        factDependencyIndex[fact] = new List<string>();
                    }
                    // Add dependencies
                    factDependencyIndex[fact].Add("SomeRuleName");
                }
            }
        }

        private List<Instruction> OptimizeInstructions(List<Instruction> instructions)
        {
            var optimizedInstructions = new List<Instruction>();

            for (int i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];

                // Combine JUMP_IF_TRUE and JUMP_IF_FALSE if they are adjacent
                if (
                    instruction.Opcode == Opcode.JUMP_IF_FALSE
                    && i + 1 < instructions.Count
                    && instructions[i + 1].Opcode == Opcode.JUMP_IF_TRUE
                )
                {
                    optimizedInstructions.Add(new Instruction { Opcode = Opcode.NOP });
                    i++; // Skip the next instruction (JUMP_IF_TRUE)
                }
                else
                {
                    optimizedInstructions.Add(instruction);
                }
            }

            // Remove unused labels
            return optimizedInstructions.FindAll(instr => instr.Opcode != Opcode.LABEL);
        }
    }
}
