using System;
using System.Diagnostics;
using CustomBuildTask.CMake;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CustomBuildTask;

public class NativeCompilationTask : Microsoft.Build.Utilities.Task
{
    public static readonly TargetOS CurrentOS;
    public static string RootDir = "";
    public const string OUTPUT_DIR_NAME = "Natives";
    public const string BUILD_DIR_NAME = "Build";

    static NativeCompilationTask() {
        if (OperatingSystem.IsWindows()) {
            CurrentOS = TargetOS.Windows;
        } else if (OperatingSystem.IsLinux()) {
            CurrentOS = TargetOS.Linux;
        } else if (OperatingSystem.IsMacOS()) {
            CurrentOS = TargetOS.MacOS;
        } else {
            throw new NotSupportedException("Your OS is unsupported");
        }
    }

    public override bool Execute()
    {
        RootDir = Path.GetDirectoryName(this.BuildEngine.ProjectFileOfTaskNode!)!;
        if (!FileEx.Exists("cmake")) {
            throw new Exception("cmake is not installed. Cannot proceed.");
        }
        List<TargetOS> buildList = new();
        foreach (var item in Enum.GetValues<TargetOS>())
        {
            if (CanCompileFor(item)) {
                buildList.Add(item);
            }
        }

        foreach (var item in buildList)
        {
            try
            {
                this.CompileFor(item);
            }
            catch (CompileException e)
            {
                Log.LogError("Building natives failed with error " + e.Message);
                return false;
            }
        }

        return true;
    }

    private bool CanCompileFor(TargetOS targetOS) {
        switch (targetOS)
        {
            case TargetOS.Windows:
                if (CurrentOS == TargetOS.Linux || CurrentOS == TargetOS.MacOS) {
                    return FileEx.Exists("x86_64-w64-mingw32-gcc") && FileEx.Exists("ldd");
                } else if (CurrentOS == TargetOS.Windows) {
                    return FileEx.Exists("cl") && FileEx.Exists("link");
                }
                break;
            case TargetOS.Linux:
                if (CurrentOS == TargetOS.Linux || CurrentOS == TargetOS.MacOS) {
                    return (FileEx.Exists("g++") || FileEx.Exists("gcc") || FileEx.Exists("ninja")) && FileEx.Exists("ldd");
                }
                break;
            case TargetOS.MacOS:
                if (CurrentOS == TargetOS.Linux) {
                    return FileEx.Exists("osxcross-conf");
                }

                if (CurrentOS == TargetOS.MacOS) {
                    return FileEx.Exists("clang") || FileEx.Exists("gcc");
                }
                break;
        }

        return false;
    }

    private static string GetOSXCrossVar(string key) {
        var osxcrossConfPath = FileEx.GetFullPath("osxcross-conf");
        if (osxcrossConfPath == null) {
            throw new Exception("osxcross not installed");
        }

        Process proc = new()
        {
            StartInfo = new()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                FileName = osxcrossConfPath
            }
        };

        proc.Start();

        string? lastLine;
        while (!proc.StandardOutput.EndOfStream) 
        {
            lastLine = proc.StandardOutput.ReadLine();
            if (lastLine != null) {
                string targetStr = $"export {key}=";
                if (lastLine.Contains(targetStr)) {
                    proc.Kill();
                    return lastLine.Remove(0, targetStr.Length);
                }
            }
        }

        proc.WaitForExit();
        throw new Exception("Getting OSXCross version failed");
    }

    private void CompileFor(TargetOS targetOS) {
        string osStr = GetStringForOS(targetOS);
        this.Log.LogMessage(MessageImportance.High, $"Attempting to build natives for '{osStr}'");
        bool isCrossCompile = targetOS != CurrentOS;
        string builddir64 = Path.Combine(RootDir, BUILD_DIR_NAME, osStr, "x64");
        string builddir32 = Path.Combine(RootDir, BUILD_DIR_NAME, osStr, "x32");
        string outputdir = Path.Combine(RootDir, OUTPUT_DIR_NAME, osStr);
        Directory.CreateDirectory(builddir64);
        Directory.CreateDirectory(builddir32);
        Directory.CreateDirectory(outputdir);
        this.Log.LogMessage(MessageImportance.High, $"IsCrossCompile: '{isCrossCompile}'");
        string compilerIdentity64 = "";
        string compilerIdentity32 = "";
        string compilerFlags64 = "";
        string compilerFlags32 = "";

        switch (targetOS)
        {
            case TargetOS.Windows:
                if (CurrentOS == TargetOS.Linux) {
                    compilerIdentity64 = "MingW64";
                    compilerIdentity32 = "MingW32";
                }

                if (CurrentOS == TargetOS.Windows) {
                    compilerFlags32 = "-DCMAKE_GENERATOR_PLATFORM=x86 ";
                }
                break;

            case TargetOS.Linux:
                if (CurrentOS == TargetOS.Linux) {
                    compilerIdentity32 = "GCC32";
                }
                break;

            case TargetOS.MacOS:
                if (CurrentOS == TargetOS.Linux) {
                    string osxcrossversion = GetOSXCrossVar("OSXCROSS_TARGET");
                    string osxcrosstarget = Path.GetFullPath(GetOSXCrossVar("OSXCROSS_TARGET_DIR"));
                    compilerFlags32 = $"-DOSXCROSS_TARGET={osxcrossversion} -DOSXCROSS_TARGET_DIR=\"{osxcrosstarget}\" ";
                    compilerFlags64 = $"-DOSXCROSS_TARGET={osxcrossversion} -DOSXCROSS_TARGET_DIR=\"{osxcrosstarget}\" ";
                    compilerIdentity64 = "osxcross";
                    compilerIdentity32 = "osxcross";
                }
                break;
        }

        if (!string.IsNullOrEmpty(compilerIdentity64)) {
            compilerFlags64 += $"-DCMAKE_TOOLCHAIN_FILE=\"{RootDir}/cmake/{compilerIdentity64}.cmake\"";
        }

        if (!string.IsNullOrEmpty(compilerIdentity32)) {
            compilerFlags32 += $"-DCMAKE_TOOLCHAIN_FILE=\"{RootDir}/cmake/{compilerIdentity32}.cmake\"";
        }

        this.Log.LogMessage(MessageImportance.High, $"Building x86_64 (64-bit) natives " + (string.IsNullOrEmpty(compilerIdentity64) ? "" : "with " + compilerIdentity64));
        this.RunCMake($"\"{RootDir}\" {compilerFlags64} -DBUILD_PLATFORM_TARGET={osStr} -DBUILD_BITS=\"64\" -DNATIVE_OUTPUT_FOLDER=\"{outputdir}\"", builddir64);
        this.RunCMake("--build .", builddir64);

        if (targetOS == TargetOS.MacOS) {
            this.Log.LogMessage(MessageImportance.High, $"Building arm64 natives " + (string.IsNullOrEmpty(compilerIdentity32) ? "" : "with " + compilerIdentity32));
            this.RunCMake($"\"{RootDir}\" {compilerFlags32} -DBUILD_PLATFORM_TARGET={osStr} -DBUILD_BITS=\"ARM\" -DNATIVE_OUTPUT_FOLDER=\"{outputdir}\"", builddir32);
            this.RunCMake("--build .", builddir32);
        } else {
            this.Log.LogMessage(MessageImportance.High, $"Building x86 (32-bit) natives " + (string.IsNullOrEmpty(compilerIdentity32) ? "" : "with " + compilerIdentity32));
            this.RunCMake($"\"{RootDir}\" {compilerFlags32} -DBUILD_PLATFORM_TARGET={osStr} -DBUILD_BITS=\"32\" -DNATIVE_OUTPUT_FOLDER=\"{outputdir}\"", builddir32);
            this.RunCMake("--build .", builddir32);
        }
        
    }

    public static string GetStringForOS(TargetOS os) {
        return os switch
        {
            TargetOS.Windows => "windows",
            TargetOS.MacOS => "macos",
            TargetOS.Linux => "linux",
            _ => throw new ArgumentOutOfRangeException(nameof(os)),
        };
    }

    public void RunCMake(string args, string builddir) {
        Process proc = new()
        {
            StartInfo = new("cmake", args)
            {
                WorkingDirectory = builddir,
            }
        };

        this.Log.LogCommandLine("cmake " + args);
        proc.Start();
        proc.WaitForExit();
        if (proc.ExitCode != 0) {
            throw new CompileException("cmake exited with error " + proc.ExitCode);
        }
    }
}