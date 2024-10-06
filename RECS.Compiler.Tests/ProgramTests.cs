// RECS/Compiler.Tests/ProgramTests.cs

// Unit Tests for RECS Compiler Entry Point
// File: ProgramTests.cs

namespace RECS.Compiler.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void Main_ShouldDisplayUsage_WhenArgumentsAreInsufficient()
        {
            using (var consoleOutput = new StringWriter())
            {
                Console.SetOut(consoleOutput);

                // Run Main with insufficient arguments
                Program.Main(new string[] { });

                var output = consoleOutput.ToString();
                Assert.Contains("Usage: RECSCompiler", output);
            }
        }

        [Fact]
        public void Main_ShouldValidateOnly_WhenValidateOnlyOptionIsProvided()
        {
            string tempRulesetPath = Path.GetTempFileName();
            File.WriteAllText(
                tempRulesetPath,
                "{\"rules\": [{\"name\": \"TestRule\", \"priority\": 1, \"conditions\": {\"all\": [{\"fact\": \"temperature\", \"operator\": \"GT\", \"value\": 30}]}, \"actions\": []}]}"
            ); // Minimal valid ruleset for testing

            using (var consoleOutput = new StringWriter())
            {
                Console.SetOut(consoleOutput);

                // Run Main with validate-only option
                Program.Main(new string[] { tempRulesetPath, "output.bin", "--validate-only" });

                var output = consoleOutput.ToString();
                Assert.Contains("Ruleset validation completed successfully.", output);
            }

            File.Delete(tempRulesetPath);
        }

        [Fact]
        public void Main_ShouldGenerateBytecode_WhenValidRulesetIsProvided()
        {
            string tempRulesetPath = Path.GetTempFileName();
            string tempOutputPath = Path.GetTempFileName();

            // Example valid ruleset for testing
            string validRulesetJson =
                "{\"rules\": [{\"name\": \"TestRule\", \"priority\": 1, \"conditions\": {\"all\": [{\"fact\": \"temperature\", \"operator\": \"GT\", \"value\": 30}]}, \"actions\": [{\"type\": \"print\", \"target\": \"console\", \"value\": \"Temperature exceeds limit\"}]}]}";

            File.WriteAllText(tempRulesetPath, validRulesetJson);

            using (var consoleOutput = new StringWriter())
            {
                Console.SetOut(consoleOutput);

                // Run Main with valid ruleset
                Program.Main(new string[] { tempRulesetPath, tempOutputPath });

                var output = consoleOutput.ToString();
                Assert.Contains("Compilation completed successfully.", output);

                // Verify output file exists and is not empty
                Assert.True(File.Exists(tempOutputPath));
                Assert.True(new FileInfo(tempOutputPath).Length > 0);
            }

            File.Delete(tempRulesetPath);
            File.Delete(tempOutputPath);
        }

        [Fact]
        public void Main_ShouldOptimizeBytecode_WhenOptimizeOptionIsProvided()
        {
            string tempRulesetPath = Path.GetTempFileName();
            string tempOutputPath = Path.GetTempFileName();

            // Example valid ruleset for testing
            string validRulesetJson =
                "{\"rules\": [{\"name\": \"TestRule\", \"priority\": 1, \"conditions\": {\"all\": [{\"fact\": \"temperature\", \"operator\": \"GT\", \"value\": 30}]}, \"actions\": [{\"type\": \"print\", \"target\": \"console\", \"value\": \"Temperature exceeds limit\"}]}]}";

            File.WriteAllText(tempRulesetPath, validRulesetJson);

            using (var consoleOutput = new StringWriter())
            {
                Console.SetOut(consoleOutput);

                // Run Main with optimize option
                Program.Main(
                    new string[] { tempRulesetPath, tempOutputPath, "--optimize", "--verbose" }
                );

                var output = consoleOutput.ToString();
                Assert.Contains("Optimizing bytecode...", output);
                Assert.Contains("Compilation completed successfully.", output);

                // Verify output file exists and is not empty
                Assert.True(File.Exists(tempOutputPath));
                Assert.True(new FileInfo(tempOutputPath).Length > 0);
            }

            File.Delete(tempRulesetPath);
            File.Delete(tempOutputPath);
        }

        [Fact]
        public void Main_ShouldDisplayError_WhenInvalidRulesetIsProvided()
        {
            string tempRulesetPath = Path.GetTempFileName();
            File.WriteAllText(tempRulesetPath, "{invalid_json}"); // Invalid JSON for testing

            using (var consoleOutput = new StringWriter())
            {
                Console.SetOut(consoleOutput);

                // Run Main with invalid ruleset
                Program.Main(new string[] { tempRulesetPath, "output.bin" });

                var output = consoleOutput.ToString();
                Assert.Contains("Error: Failed to parse JSON data.", output);
            }

            File.Delete(tempRulesetPath);
        }
    }
}
