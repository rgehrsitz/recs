using RECS.Compiler;

namespace RECS.Engine.Tests
{
    public class RuleEngineTests
    {
        [Fact]
        public void Execute_SimpleRule_PrintsMessage()
        {
            var bytecode = new List<Instruction>
            {
                new() { Opcode = Opcode.JumpIfFalse, Operands = [.. "temperature>30"u8.ToArray()] },
                new() { Opcode = Opcode.ExecuteAction, Operands = [.. "print"u8.ToArray()] },
            };

            var engine = new RuleEngine();
            engine.LoadBytecode(bytecode);
            engine.SetFact("temperature", 35);

            // Capture the console output
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);
            engine.Execute();

            Assert.Contains("Executing action: Print to console.", consoleOutput.ToString());
        }

        [Fact]
        public void Execute_JumpIfFalse_SkipsAction()
        {
            var bytecode = new List<Instruction>
            {
                new() { Opcode = Opcode.JumpIfFalse, Operands = [.. "temperature>30"u8.ToArray()] },
                new() { Opcode = Opcode.ExecuteAction, Operands = [.. "print"u8.ToArray()] },
            };

            var engine = new RuleEngine();
            engine.LoadBytecode(bytecode);
            engine.SetFact("temperature", 25);

            // Capture the console output
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);
            engine.Execute();

            Assert.DoesNotContain("Executing action: Print to console.", consoleOutput.ToString());
        }
    }
}
