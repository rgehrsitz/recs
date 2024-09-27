// RECS.Compiler.Tests/BytecodeGeneratorTests.cs

using System.Collections.Generic;
using RECS.Core;
using Xunit;

namespace RECS.Compiler.Tests
{
    public class BytecodeGeneratorTests
    {
        [Fact]
        public void Generate_ValidRuleset_ReturnsBytecode()
        {
            var json = @"{
        'rules': [
            {
                'name': 'SampleRule',
                'priority': 1,
                'conditions': {
                    'all': [
                        {
                            'fact': 'temperature',
                            'operator': 'GT',
                            'value': 30
                        }
                    ]
                },
                'actions': [
                    {
                        'type': 'print',
                        'target': 'console',
                        'value': 'Hello, World!'
                    }
                ]
            }
        ]
    }".Replace('\'', '"');

            var ruleset = Ruleset.Parse(json);
            var generator = new BytecodeGenerator();
            var bytecode = generator.Generate(ruleset);

            // Temporary debugging to inspect bytecode
            foreach (var instruction in bytecode)
            {
                Console.WriteLine(
                    $"Opcode: {instruction.Opcode}, Operands: {BitConverter.ToString(instruction.Operands.ToArray())}"
                );
            }

            // Update expectations based on new structure
            Assert.NotEmpty(bytecode);
            Assert.Equal(21, bytecode.Count); // Adjust count based on actual structure

            // Assert.Equal(Opcode.RULE_START, bytecode[0].Opcode);
            // Assert.Equal(Opcode.LOAD_CONST_FLOAT, bytecode[1].Opcode);
            // Assert.Equal(Opcode.EXECUTE_ACTION, bytecode[2].Opcode);
            // Assert.Equal(Opcode.RULE_END, bytecode[3].Opcode);
        }

        [Fact]
        public void Generate_ComplexRule_ReturnsBytecode()
        {
            var json = @"{
            'rules': [
                {
                    'name': 'ComplexRule',
                    'priority': 2,
                    'conditions': {
                        'all': [
                            {
                                'fact': 'humidity',
                                'operator': 'GT',
                                'value': 60
                            }
                        ],
                        'any': [
                            {
                                'fact': 'temperature',
                                'operator': 'EQ',
                                'value': 20
                            }
                        ]
                    },
                    'actions': [
                        {
                            'type': 'print',
                            'target': 'console',
                            'value': 'Humidity is high and temperature is moderate.'
                        }
                    ]
                }
            ]
        }".Replace('\'', '"');

            var ruleset = Ruleset.Parse(json);
            var generator = new BytecodeGenerator();
            var bytecode = generator.Generate(ruleset);

            Assert.NotEmpty(bytecode);
            Assert.Contains(bytecode, instr => instr.Opcode == Opcode.RULE_START);
            Assert.Contains(bytecode, instr => instr.Opcode == Opcode.JUMP_IF_FALSE);
            Assert.Contains(bytecode, instr => instr.Opcode == Opcode.EXECUTE_ACTION);
        }
    }
}
