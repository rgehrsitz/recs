using System;
using System.Collections.Generic;
using RECS.Compiler;
using RECS.Core;
using Xunit;

namespace RECS.Engine.Tests
{
    public class RuleEngineTests
    {
        [Fact]
        public void Execute_SimpleRule_PrintsMessage()
        {
            var bytecode = new List<Instruction>
            {
                new Instruction
                {
                    Opcode = Opcode.JumpIfFalse,
                    Operands = new List<byte>(System.Text.Encoding.UTF8.GetBytes("temperature>30")),
                },
                new Instruction
                {
                    Opcode = Opcode.ExecuteAction,
                    Operands = new List<byte>(System.Text.Encoding.UTF8.GetBytes("print")),
                },
            };

            var engine = new RuleEngine();
            engine.LoadBytecode(bytecode);
            engine.SetFact("temperature", 35);

            // Capture the console output
            using (var consoleOutput = new System.IO.StringWriter())
            {
                Console.SetOut(consoleOutput);
                engine.Execute();

                Assert.Contains("Executing action: Print to console.", consoleOutput.ToString());
            }
        }

        [Fact]
        public void Execute_JumpIfFalse_SkipsAction()
        {
            var bytecode = new List<Instruction>
            {
                new Instruction
                {
                    Opcode = Opcode.JumpIfFalse,
                    Operands = new List<byte>(System.Text.Encoding.UTF8.GetBytes("temperature>30")),
                },
                new Instruction
                {
                    Opcode = Opcode.ExecuteAction,
                    Operands = new List<byte>(System.Text.Encoding.UTF8.GetBytes("print")),
                },
            };

            var engine = new RuleEngine();
            engine.LoadBytecode(bytecode);
            engine.SetFact("temperature", 25);

            // Capture the console output
            using (var consoleOutput = new System.IO.StringWriter())
            {
                Console.SetOut(consoleOutput);
                engine.Execute();

                Assert.DoesNotContain(
                    "Executing action: Print to console.",
                    consoleOutput.ToString()
                );
            }
        }
    }
}
