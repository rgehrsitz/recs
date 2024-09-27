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
        private Dictionary<string, int> pendingLabelReplacements = new Dictionary<string, int>();

        public List<Instruction> Generate(Ruleset ruleset)
        {
            var instructions = new List<Instruction>();

            foreach (var rule in ruleset.Rules)
            {
                instructions.Add(new Instruction { Opcode = Opcode.RULE_START });

                // Encode rule name and priority
                instructions.AddRange(EncodeString(rule.Name));
                instructions.Add(new Instruction { Opcode = Opcode.PRIORITY });
                instructions.AddRange(EncodeInteger(rule.Priority));

                // Generate conditions and actions
                var conditionNode = ConvertConditionGroupToNode(rule.Conditions);
                instructions.AddRange(GenerateInstructionsForConditions(conditionNode));

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

        private List<Instruction> GenerateInstructionsForConditions(ConditionOrGroup conditionNode)
        {
            var instructions = new List<Instruction>();

            if (conditionNode.Fact != null)
            {
                var label = "L" + labelCounter++;
                pendingLabelReplacements[label] = instructions.Count;
                instructions.Add(GenerateConditionInstruction(conditionNode, false));
            }

            foreach (var condition in conditionNode.All ?? new List<ConditionOrGroup>())
            {
                instructions.AddRange(GenerateInstructionsForConditions(condition));
            }

            foreach (var condition in conditionNode.Any ?? new List<ConditionOrGroup>())
            {
                instructions.AddRange(GenerateInstructionsForConditions(condition));
            }

            return instructions;
        }

        private void ReplaceLabels(List<Instruction> instructions)
        {
            foreach (var label in pendingLabelReplacements)
            {
                var labelIndex = label.Value;
                var labelPosition = labelPositions[label.Key];

                // Replace label with actual offset in instruction operands
                var offsetBytes = BitConverter.GetBytes(labelPosition - labelIndex);
                instructions[labelIndex].Operands.AddRange(offsetBytes);
            }
        }

        private Instruction GenerateConditionInstruction(
            ConditionOrGroup condition,
            bool isAnyCondition
        )
        {
            var instruction = new Instruction
            {
                Opcode = isAnyCondition ? Opcode.JUMP_IF_TRUE : Opcode.JUMP_IF_FALSE,
            };

            // Add fact
            var factBytes = Encoding.UTF8.GetBytes(condition.Fact ?? string.Empty);
            instruction.Operands.AddRange(factBytes);

            // Add operator
            switch (condition.Operator)
            {
                case "GT":
                    instruction.Opcode = Opcode.GT_FLOAT;
                    break;
                case "EQ":
                    instruction.Opcode = Opcode.EQ_FLOAT;
                    break;
                case "NEQ":
                    instruction.Opcode = Opcode.NEQ_FLOAT;
                    break;
                case "LT":
                    instruction.Opcode = Opcode.LT_FLOAT;
                    break;
                case "GTE":
                    instruction.Opcode = Opcode.GTE_FLOAT;
                    break;
                case "LTE":
                    instruction.Opcode = Opcode.LTE_FLOAT;
                    break;
                case "CONTAINS":
                    instruction.Opcode = Opcode.CONTAINS_STRING;
                    break;
                case "NOT_CONTAINS":
                    instruction.Opcode = Opcode.NOT_CONTAINS_STRING;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported operator: {condition.Operator}"
                    );
            }

            // Add value
            var valueBytes = Encoding.UTF8.GetBytes(condition.Value?.ToString() ?? string.Empty);
            instruction.Operands.AddRange(valueBytes);

            return instruction;
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
        private List<Instruction> EncodeString(string str)
        {
            var instructions = new List<Instruction>();
            var length = str.Length > 255 ? 255 : str.Length;

            instructions.Add(new Instruction { Opcode = (Opcode)length });
            var stringBytes = Encoding.UTF8.GetBytes(str.Substring(0, length));
            instructions.AddRange(stringBytes.Select(b => new Instruction { Opcode = (Opcode)b }));

            return instructions;
        }

        // Utility method to encode integers into bytecode
        private List<Instruction> EncodeInteger(int value)
        {
            var bytes = BitConverter.GetBytes(value);
            return bytes.Select(b => new Instruction { Opcode = (Opcode)b }).ToList();
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
