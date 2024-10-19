// RECS.Compiler.Tests/BytecodeGeneratorTests.cs

using System.Text;
using RECS.Core;
using Serilog;
using Serilog.Sinks.TestCorrelator;

namespace RECS.Compiler.Tests
{
    public class BytecodeGeneratorTests
    {
        public BytecodeGeneratorTests()
        {
            // Configure Serilog for testing purposes
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console() // Add console output for visibility
                .WriteTo.File("logs/recs_tests.log", rollingInterval: RollingInterval.Day) // Add file sink to store logs
                .WriteTo.TestCorrelator() // Use TestCorrelator for assertions
                .CreateLogger();
        }

        [Fact]
        public void Generate_ValidRuleset_ShouldLogBytecodeGeneration()
        {
            using (TestCorrelator.CreateContext())
            {
                var ruleset = new Ruleset
                {
                    Rules =
                    [
                        new()
                        {
                            Name = "TestRule",
                            Priority = 1,
                            Conditions = new ConditionGroup
                            {
                                All =
                                [
                                    new()
                                    {
                                        Fact = "temperature",
                                        Operator = "GT",
                                        Value = 30,
                                    },
                                ],
                            },
                            Actions =
                            [
                                new()
                                {
                                    Type = "print",
                                    Target = "console",
                                    Value = "Temperature is greater than 30",
                                },
                            ],
                        },
                    ],
                };

                var generator = new BytecodeGenerator();
                var bytecode = generator.Generate(ruleset);

                // Explicitly flush the logs
                Log.CloseAndFlush();

                // Assert that logs contain specific messages
                Assert.NotEmpty(bytecode);
                Assert.Contains(
                    TestCorrelator.GetLogEventsFromCurrentContext(),
                    logEvent =>
                        logEvent.MessageTemplate.Text.Contains(
                            "Generating bytecode for rule: TestRule"
                        )
                );
                Assert.Contains(
                    TestCorrelator.GetLogEventsFromCurrentContext(),
                    logEvent =>
                        logEvent.MessageTemplate.Text.Contains(
                            "Adding action: print to target console"
                        )
                );
            }
        }

        [Fact]
        public void ReplaceLabels_ShouldLogLabelReplacement()
        {
            using (TestCorrelator.CreateContext())
            {
                var generator = new BytecodeGenerator();
                var instructions = new List<Instruction>
                {
                    new()
                    {
                        Opcode = Opcode.Label,
                        Operands = new List<byte>(System.Text.Encoding.UTF8.GetBytes("L001")),
                    },
                    new()
                    {
                        Opcode = Opcode.JumpIfFalse,
                        Operands = new List<byte>(BitConverter.GetBytes(0)),
                    },
                };

                generator.ReplaceLabels(instructions);

                // Explicitly flush the logs
                Log.CloseAndFlush();

                // Assert that logs contain label replacement
                Assert.Contains(
                    TestCorrelator.GetLogEventsFromCurrentContext(),
                    logEvent =>
                        logEvent.MessageTemplate.Text.Contains(
                            "Replacing labels with actual offsets"
                        )
                );
            }
        }

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
            // foreach (var instruction in bytecode)
            // {
            //     _testOutputHelper.WriteLine(
            //         $"Opcode: {instruction.Opcode}, Operands: {BitConverter.ToString(instruction.Operands.ToArray())}"
            //     );
            // }


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
            Assert.Contains(bytecode, instr => instr.Opcode == Opcode.RuleStart);
            Assert.Contains(bytecode, instr => instr.Opcode == Opcode.JumpIfFalse);
            Assert.Contains(bytecode, instr => instr.Opcode == Opcode.ExecuteAction);
        }

        [Fact]
        public void ReplaceLabels_ShouldCorrectlyReplaceLabelOffsets()
        {
            // Arrange
            var generator = new BytecodeGenerator();
            var instructions = new List<Instruction>
            {
                new()
                {
                    Opcode = Opcode.JumpIfFalse,
                    Operands = new List<byte>(Encoding.UTF8.GetBytes("L001")),
                },
                new()
                {
                    Opcode = Opcode.Label,
                    Operands = new List<byte>(Encoding.UTF8.GetBytes("L001")),
                },
            };

            // Simulate pending label replacement
            generator.PendingLabelReplacements["L001"] = 0; // Replace at index 0 (JUMP_IF_FALSE)

            // Act
            generator.ReplaceLabels(instructions);

            // Assert: Label should be replaced with correct offset
            var expectedOffset = BitConverter.GetBytes(1); // LABEL is at index 1, JUMP_IF_FALSE at index 0
            Assert.Equal(expectedOffset, instructions[0].Operands.GetRange(0, 3));
        }

        [Fact]
        public void EncodeString_ShouldEncodeCorrectlyWithLength()
        {
            // Arrange
            var generator = new BytecodeGenerator();
            var testString = "test";

            // Act
            var instructions = generator.EncodeString(testString);

            // Assert: Check that the string is encoded with its length
            var lengthInstruction = instructions[0];
            var stringInstruction = instructions[1];

            Assert.Equal(Opcode.LoadConstString, lengthInstruction.Opcode);
            Assert.Equal(
                (ushort)testString.Length,
                BitConverter.ToUInt16(lengthInstruction.Operands.ToArray())
            );

            Assert.Equal(Opcode.LoadConstString, stringInstruction.Opcode);
            Assert.Equal(testString, Encoding.UTF8.GetString(stringInstruction.Operands.ToArray()));
        }

        [Fact]
        public void EncodeInteger_ShouldEncodeCorrectly()
        {
            // Arrange
            var generator = new BytecodeGenerator();
            var testInteger = 42;

            // Act
            var instructions = BytecodeGenerator.EncodeInteger(testInteger);

            // Assert: Check that the integer is encoded properly
            var integerInstruction = instructions[0];
            Assert.Equal(Opcode.LoadConstFloat, integerInstruction.Opcode);
            Assert.Equal(testInteger, BitConverter.ToInt32(integerInstruction.Operands.ToArray()));
        }

        [Fact]
        public void GenerateConditionInstruction_ShouldGenerateCorrectInstructionBasedOnOperator()
        {
            // Arrange
            var generator = new BytecodeGenerator();
            var condition = new ConditionOrGroup
            {
                Fact = "temperature",
                Operator = "GT",
                Value = 30,
            };

            // Act
            var instruction = generator.GenerateConditionInstruction(condition, false);

            // Assert: Verify that the opcode and operands are correct
            Assert.Equal(Opcode.GtFloat, instruction.Opcode);
            Assert.Equal(
                "temperature",
                Encoding.UTF8.GetString(instruction.Operands.GetRange(0, 11).ToArray())
            );
            Assert.Equal(30, BitConverter.ToInt32(instruction.Operands.GetRange(11, 4).ToArray()));
        }

        [Fact]
        public void GenerateConditionInstruction_ShouldThrowExceptionForUnsupportedOperator()
        {
            // Arrange
            var generator = new BytecodeGenerator();
            var condition = new ConditionOrGroup
            {
                Fact = "unsupported",
                Operator = "XYZ", // Unsupported operator
                Value = 100,
            };

            // Act & Assert: Ensure an exception is thrown for unsupported operators
            Assert.Throws<InvalidOperationException>(
                () => generator.GenerateConditionInstruction(condition, false)
            );
        }

        [Fact]
        public void Generate_ShouldSkipRedundantRule_WithEmptyConditions()
        {
            var ruleset = new Ruleset
            {
                Rules =
                [
                    new()
                    {
                        Name = "EmptyConditionsRule",
                        Priority = 1,
                        Conditions =
                            new ConditionGroup() // No conditions defined
                        ,
                    },
                ],
            };

            var generator = new BytecodeGenerator();
            var bytecode = generator.Generate(ruleset);

            // Assert that no instructions were generated for the redundant rule
            Assert.Empty(bytecode);
        }

        [Fact]
        public void Generate_ShouldSkipRedundantRule_WithAlwaysTrueCondition()
        {
            var ruleset = new Ruleset
            {
                Rules =
                [
                    new()
                    {
                        Name = "AlwaysTrueConditionRule",
                        Priority = 1,
                        Conditions = new ConditionGroup
                        {
                            All =
                            [
                                new()
                                {
                                    Fact = "temperature",
                                    Operator = "EQ",
                                    Value =
                                        "temperature" // Always true condition
                                    ,
                                },
                            ],
                        },
                    },
                ],
            };

            var generator = new BytecodeGenerator();
            var bytecode = generator.Generate(ruleset);

            // Assert that no instructions were generated for the redundant rule
            Assert.Empty(bytecode);
        }

        [Fact]
        public void Generate_ShouldIncludeRule_WithValidConditions()
        {
            var ruleset = new Ruleset
            {
                Rules =
                [
                    new()
                    {
                        Name = "ValidConditionRule",
                        Priority = 1,
                        Conditions = new ConditionGroup
                        {
                            All =
                            [
                                new()
                                {
                                    Fact = "temperature",
                                    Operator = "GT",
                                    Value = 30,
                                },
                            ],
                        },
                        Actions =
                        [
                            new()
                            {
                                Type = "print",
                                Target = "console",
                                Value = "Temperature is greater than 30",
                            },
                        ],
                    },
                ],
            };

            var generator = new BytecodeGenerator();
            var bytecode = generator.Generate(ruleset);

            // Assert that instructions were generated for the valid rule
            Assert.NotEmpty(bytecode);
            Assert.Contains(bytecode, instr => instr.Opcode == Opcode.RuleStart);
            Assert.Contains(bytecode, instr => instr.Opcode == Opcode.RuleEnd);
        }

        [Fact]
        public void Generate_ShouldSkipRedundantAction_WithEmptyActionTarget()
        {
            var ruleset = new Ruleset
            {
                Rules =
                [
                    new()
                    {
                        Name = "EmptyActionTargetRule",
                        Priority = 1,
                        Conditions = new ConditionGroup
                        {
                            All =
                            [
                                new()
                                {
                                    Fact = "temperature",
                                    Operator = "GT",
                                    Value = 30,
                                },
                            ],
                        },
                        Actions =
                        [
                            new()
                            {
                                Type = "print",
                                Target =
                                    "" // Redundant action with empty target
                                ,
                            },
                        ],
                    },
                ],
            };

            var generator = new BytecodeGenerator();
            var bytecode = generator.Generate(ruleset);

            // Assert that the action was skipped, but the rule instructions were generated
            Assert.NotEmpty(bytecode);
            Assert.Contains(bytecode, instr => instr.Opcode == Opcode.RuleStart);
            Assert.Contains(bytecode, instr => instr.Opcode == Opcode.RuleEnd);
            Assert.DoesNotContain(bytecode, instr => instr.Opcode == Opcode.ExecuteAction);
        }
    }
}
