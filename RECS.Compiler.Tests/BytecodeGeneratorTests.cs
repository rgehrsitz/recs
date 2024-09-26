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
                                    'operator': '>',
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

            Assert.NotEmpty(bytecode);
            Assert.Equal(2, bytecode.Count);  // One instruction for the condition, one for the action
            Assert.Equal(Opcode.JUMP_IF_FALSE, bytecode[0].Opcode);
            Assert.Equal(Opcode.EXECUTE_ACTION, bytecode[1].Opcode);
        }
    }
}
