﻿// RECS Compiler Entry Point with Serilog Integration
// File: Program.cs

using RECS.Core;
using Serilog;

namespace RECS.Compiler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/recs_compiler.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("RECS Compiler started.");

            if (args.Length < 2)
            {
                Log.Error("Usage: RECSCompiler <inputRuleset.json> <outputBytecode.bin> [options]");
                Log.Information("Options:");
                Log.Information("  --optimize       Optimize the generated bytecode");
                Log.Information("  --verbose        Enable verbose logging");
                Log.Information(
                    "  --validate-only  Only validate the ruleset without generating bytecode"
                );
                return;
            }

            string inputFilePath = args[0];
            string outputFilePath = args[1];
            bool optimize = args.Contains("--optimize");
            bool verbose = args.Contains("--verbose");
            bool validateOnly = args.Contains("--validate-only");

            try
            {
                // Load ruleset from JSON file
                if (verbose)
                    Log.Debug($"Loading ruleset from {inputFilePath}...");
                string jsonData = File.ReadAllText(inputFilePath);
                var ruleset = Ruleset.Parse(jsonData);

                // Validate-only mode
                if (validateOnly)
                {
                    Log.Information("Ruleset validation completed successfully.");
                    return;
                }

                // Generate bytecode using BytecodeGenerator
                if (verbose)
                    Log.Debug("Generating bytecode...");
                var bytecodeGenerator = new BytecodeGenerator();
                var bytecode = bytecodeGenerator.Generate(ruleset);

                // Optimize bytecode if requested
                if (optimize)
                {
                    if (verbose)
                        Log.Debug("Optimizing bytecode...");
                    bytecode = OptimizeBytecode(bytecode);
                }

                // Serialize bytecode to binary file
                if (verbose)
                    Log.Debug($"Saving bytecode to {outputFilePath}...");
                using (
                    var fileStream = new FileStream(
                        outputFilePath,
                        FileMode.Create,
                        FileAccess.Write
                    )
                )
                using (var binaryWriter = new BinaryWriter(fileStream))
                {
                    foreach (var instruction in bytecode)
                    {
                        binaryWriter.Write((byte)instruction.Opcode);
                        binaryWriter.Write(instruction.Operands.ToArray());
                    }
                }

                Log.Information("Compilation completed successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex.Message}");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        static List<Instruction> OptimizeBytecode(List<Instruction> bytecode)
        {
            // Placeholder for bytecode optimization logic
            var optimizedBytecode = new List<Instruction>();
            foreach (var instruction in bytecode)
            {
                // Example optimization: remove redundant NOPs
                if (instruction.Opcode != Opcode.Nop)
                {
                    optimizedBytecode.Add(instruction);
                }
            }
            return optimizedBytecode;
        }
    }
}
