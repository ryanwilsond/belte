using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Buckle;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Profiling;

public static partial class Profiler {
    private static CompilationOptions DefaultOptions
        = new CompilationOptions(BuildMode.None, OutputKind.ConsoleApplication);

    private static Compilation CorLibrary;

    public static void Run(string[] args) {
        var resultFile = File.CreateText("profiler_results.log");

        // If anything unexpected happens, we don't want to waste the collected data
        try {
            CorLibrary = LibraryHelpers.LoadLibraries();
            var corDiagnostics = CorLibrary.GetDiagnostics();
            Debug.Assert(corDiagnostics.Count == 0);

            for (var i = 0; i < TestCases.Length; i++) {
                var testCase = TestCases[i];

                Console.WriteLine($"Running case {i + 1}...");

                resultFile.WriteLine();
                resultFile.WriteLine();
                resultFile.WriteLine($"Case {i + 1}:");
                resultFile.WriteLine();
                resultFile.WriteLine("```");
                resultFile.WriteLine(testCase);
                resultFile.WriteLine("```");
                resultFile.WriteLine();

                var totalCase = new CaseResult();
                var eBts = new List<int>();
                var eEMts = new List<int>();
                var eEXts = new List<int>();
                // var iIts = new List<int>();
                var evBts = new List<int>();
                var evEts = new List<int>();

                var eFailed = false;
                var iFailed = false;
                var evFailed = false;

                for (var j = 0; j < 20; j++) {
                    var result = RunCase(testCase, eFailed, iFailed, evFailed);

                    if (result.eE != default) {
                        resultFile.WriteLine($"Executor failed: Case {i + 1} Run {j + 1}");
                        resultFile.WriteLine(result.eE);
                        eFailed = true;
                    } else {
                        eBts.Add(result.eBt);
                        eEMts.Add(result.eEMt);
                        eEXts.Add(result.eEXt);
                    }

                    // if (result.iE != default) {
                    //     resultFile.WriteLine($"Interpreter failed: Case {i + 1} Run {j + 1}");
                    //     resultFile.WriteLine(result.iE);
                    //     iFailed = true;
                    // } else {
                    //     iIts.Add(result.iIt);
                    // }

                    if (result.evE != default) {
                        resultFile.WriteLine($"Evaluator failed: Case {i + 1} Run {j + 1}");
                        resultFile.WriteLine(result.evE);
                        evFailed = true;
                    } else {
                        evBts.Add(result.evBt);
                        evEts.Add(result.evEt);
                    }

                    if (j >= 3) {
                        var mean = evEts.Average();
                        var stddev = Math.Sqrt(evEts.Select(t => Math.Pow(t - mean, 2)).Average());

                        if (stddev / mean < 0.02)
                            break;
                    }
                }

                var eBt = eBts.Count != 0 ? eBts.Average() : 0;
                var eEMt = eEMts.Count != 0 ? eEMts.Average() : 0;
                var eEXt = eEXts.Count != 0 ? eEXts.Average() : 0;
                // var iIt = iIts.Count != 0 ? iIts.Average() : 0;
                var evBt = evBts.Count != 0 ? evBts.Average() : 0;
                var evEt = evEts.Count != 0 ? evEts.Average() : 0;

                resultFile.WriteLine($"Executor: {eBt + eEMt + eEXt:F3} ms");
                resultFile.WriteLine($"    Bound in ~{eBt:F3} ms");
                resultFile.WriteLine($"    Emitted in ~{eEXt:F3} ms");
                resultFile.WriteLine($"    Executed in ~{eEMt:F3} ms");
                // resultFile.WriteLine($"Interpreter: {iIt:F3} ms");
                // resultFile.WriteLine($"    Interpreted in ~{iIt:F3} ms");
                resultFile.WriteLine($"Evaluator: {evBt + evEt:F3} ms");
                resultFile.WriteLine($"    Bound in ~{evBt:F3} ms");
                resultFile.WriteLine($"    Evaluated in ~{evEt:F3} ms");
            }

            Console.WriteLine();
            Console.WriteLine("Done");
        } finally {
            resultFile.Close();
        }
    }

    private static void PrintDiagnostics(BelteDiagnosticQueue diagnostics) {
        var diagnostic = diagnostics.Pop();

        while (diagnostic is not null) {
            DiagnosticFormatter.PrettyPrint(diagnostic);
            diagnostic = diagnostics.Pop();
        }
    }

    private static CaseResult RunCase(string testCase, bool skipE, bool skipI, bool skipEV) {
        // Compilations and SyntaxTrees will cache results, so we have to really isolate each endpoint

        var result = new CaseResult();

        if (!skipE) {
            var st1 = SyntaxTree.Parse(testCase);
            var comp1 = Compilation.Create("Profiling", DefaultOptions, CorLibrary, st1);

            try {
                var executeDiagnostics = comp1.Execute(false, true).ToArray();

                if (executeDiagnostics.Length != 3) {
                    result.eE = $"Executor produced too many ({executeDiagnostics.Length}) diagnostics!";
                } else {
                    result.eBt = int.Parse(executeDiagnostics[0].message.Substring(21).Replace(" ms", ""));
                    result.eEMt = int.Parse(executeDiagnostics[1].message.Substring(23).Replace(" ms", ""));
                    result.eEXt = int.Parse(executeDiagnostics[2].message.Substring(24).Replace(" ms", ""));
                }
            } catch (Exception e) {
                result.eE = e.Message;
            }
        }

        // if (!skipI) {
        //     var st2 = SyntaxTree.Parse(testCase);
        //     var comp2 = Compilation.Create("Profiling", DefaultOptions, CorLibrary, st2);

        //     try {
        //         var interpretDiagnostics = comp2.Interpret(false, true).diagnostics.ToArray();

        //         if (interpretDiagnostics.Length != 1) {
        //             result.iE = $"Interpreter produced too many ({interpretDiagnostics.Length}) diagnostics!";
        //         } else {
        //             result.iIt = int.Parse(interpretDiagnostics[0].message.Substring(27).Replace(" ms", ""));
        //         }
        //     } catch (Exception e) {
        //         result.iE = e.Message;
        //     }
        // }

        if (!skipEV) {
            var st3 = SyntaxTree.Parse(testCase);
            var comp3 = Compilation.Create("Profiling", DefaultOptions, CorLibrary, st3);

            try {
                var evaluateDiagnostics = comp3.Evaluate(false, true).diagnostics.ToArray();


                if (evaluateDiagnostics.Length != 2) {
                    result.evE = $"Evaluator produced too many ({evaluateDiagnostics.Length}) diagnostics!";
                } else {
                    result.evBt = int.Parse(evaluateDiagnostics[0].message.Substring(21).Replace(" ms", ""));
                    result.evEt = int.Parse(evaluateDiagnostics[1].message.Substring(25).Replace(" ms", ""));
                }
            } catch (Exception e) {
                result.evE = e.Message;
            }
        }

        return result;
    }
}
