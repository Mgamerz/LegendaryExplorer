﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ME3ExplorerCore.Packages;
using ME3Script;
using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3ExplorerCore.Tests
{
    [TestClass]
    public class ScriptTests
    {
        [TestMethod]
        public void TestScript()
        {
            GlobalTest.Init();

            bool standardLibInitialized = StandardLibrary.InitializeStandardLib(
#if AZURE
                Path.Combine(GlobalTest.GetTestPackagesDirectory(), "PC", "ME3", "SFXGame.pcc")
#endif
                ).Result;

            Assert.IsTrue(standardLibInitialized, "ME3 Script standard library failed to compile!");

            using (var biopProEar = MEPackageHandler.OpenMEPackage(Path.Combine(GlobalTest.GetTestPackagesDirectory(), "PC", "ME3", "BioP_ProEar.pcc")))
            {
                var biopProEarLib = new FileLib(biopProEar);
                bool fileLibInitialized = biopProEarLib.Initialize().Result;
                Assert.IsTrue(fileLibInitialized, "ME3 Script failed to compile BioP_ProEar class definitions!");

                foreach (ExportEntry funcExport in biopProEar.Exports.Where(exp => exp.ClassName == "Function"))
                {
                    (ASTNode astNode, string text) = ME3ScriptCompiler.DecompileExport(funcExport, biopProEarLib);

                    Assert.IsInstanceOfType(astNode, typeof(Function), $"#{funcExport.UIndex} {funcExport.InstancedFullPath} in BioP_ProEar did not decompile!");

                    (_, MessageLog log) = ME3ScriptCompiler.CompileFunction(funcExport, text, biopProEarLib);

                    if (log.AllErrors.Any())
                    {
                        Assert.Fail($"#{funcExport.UIndex} {funcExport.InstancedFullPath} in BioP_ProEar did not recompile!");
                    }
                }
            }

        }
    }
}
