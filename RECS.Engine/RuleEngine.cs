using System;
using System.Collections.Generic;
using System.Text;
using RECS.Compiler;
using RECS.Core;

namespace RECS.Engine
{
    public class RuleEngine
    {
        private List<Instruction> _bytecode = new List<Instruction>();
        private Dictionary<string, object> _facts = new Dictionary<string, object>();

        public RuleEngine() { }

        public void LoadBytecode(List<Instruction> bytecode)
        {
            _bytecode = bytecode;
        }

        public void SetFact(string fact, object value)
        {
            _facts[fact] = value;
        }

        public void Execute()
        {
            for (int i = 0; i < _bytecode.Count; i++)
            {
                var instruction = _bytecode[i];

                switch (instruction.Opcode)
                {
                    case Opcode.JUMP_IF_FALSE:
                        i = HandleJump(instruction, i, false);
                        break;

                    case Opcode.JUMP_IF_TRUE:
                        i = HandleJump(instruction, i, true);
                        break;

                    case Opcode.EXECUTE_ACTION:
                        ExecuteAction(instruction);
                        break;

                    default:
                        throw new InvalidOperationException("Unknown Opcode encountered.");
                }
            }
        }

        private int HandleJump(Instruction instruction, int currentIndex, bool jumpIfTrue)
        {
            // Split operands by the separator byte (0)
            var parts = SplitOperands(instruction.Operands.ToArray());

            string fact = parts[0];
            string @operator = parts[1];
            string valueStr = parts[2];

            if (!_facts.TryGetValue(fact, out var factValue))
            {
                throw new InvalidOperationException($"Fact '{fact}' not provided.");
            }

            bool conditionMet = EvaluateCondition(factValue, @operator, valueStr);

            if ((jumpIfTrue && conditionMet) || (!jumpIfTrue && !conditionMet))
            {
                // If the condition matches, we do the jump by returning the new index
                return currentIndex + 1;
            }

            return currentIndex; // No jump
        }

        private List<string> SplitOperands(byte[] operands)
        {
            var parts = new List<string>();
            var currentPart = new List<byte>();

            foreach (var b in operands)
            {
                if (b == 0)
                {
                    // Separator byte found, add the current part to the parts list
                    parts.Add(Encoding.UTF8.GetString(currentPart.ToArray()));
                    currentPart.Clear();
                }
                else
                {
                    currentPart.Add(b);
                }
            }

            if (currentPart.Count > 0)
            {
                parts.Add(Encoding.UTF8.GetString(currentPart.ToArray()));
            }

            return parts;
        }

        private bool EvaluateCondition(object factValue, string @operator, string expectedValue)
        {
            // Basic comparison logic
            switch (@operator)
            {
                case ">":
                    return Convert.ToDouble(factValue) > Convert.ToDouble(expectedValue);
                case "<":
                    return Convert.ToDouble(factValue) < Convert.ToDouble(expectedValue);
                case "==":
                    return factValue.ToString() == expectedValue;
                default:
                    throw new InvalidOperationException(
                        $"Unknown operator '{@operator}' encountered."
                    );
            }
        }

        private void ExecuteAction(Instruction instruction)
        {
            string actionType = System.Text.Encoding.UTF8.GetString(
                instruction.Operands.ToArray(),
                0,
                instruction.Operands.Count
            );
            // Here you can define action execution logic based on the action type and other properties

            // For demonstration, we'll simulate a "print" action:
            if (actionType == "print")
            {
                Console.WriteLine("Executing action: Print to console.");
            }
        }
    }
}
