using System;
using System.Diagnostics;
using System.IO;

namespace NexCalc.Core
{
    // Executes a basic Python script by spawning the installed python.exe,
    // writing the source to a temporary .py file, and capturing stdout/stderr.
    // Kept inside the Core library so the headless regression tests can
    // exercise it, not just the UI.
    public class PythonRunner {
        public class RunResult {
            public string output = "";
            public string errorText = "";
            public int exitCode;
            public bool timedOut;
            public string? note;   // set when python.exe cannot be started
        }

        // Runs source with the PATH-resolved "python" interpreter.
        public static RunResult Run(string source, int timeoutMs) {
            return Run(source, timeoutMs, "python");
        }

        public static RunResult Run(string source, int timeoutMs, string pythonExe) {
            RunResult res = new RunResult();
            res.output = "";
            res.errorText = "";
            res.exitCode = 0;
            res.timedOut = false;
            res.note = null;

            // Probe the interpreter first so a missing python.exe produces a
            // clean note instead of a spawn exception.
            Process? probe;
            try {
                probe = StartProcess(pythonExe, "--version");
            } catch (Exception) {
                res.note = "Could not start python: '" + pythonExe + "' not found.";
                return res;
            }
            if (probe == null) {
                res.note = "Could not start python: '" + pythonExe + "' not found.";
                return res;
            }
            probe.WaitForExit();

            string scriptPath = Path.Combine(Path.GetTempPath(), "nexcalc_python_script_" + Guid.NewGuid().ToString("N") + ".py");
            File.WriteAllText(scriptPath, source);

            Process? proc;
            try {
                proc = StartProcess(pythonExe, "\"" + scriptPath + "\"");
            } catch (Exception) {
                res.note = "Could not start python: '" + pythonExe + "' not found.";
                DeleteIfExists(scriptPath);
                return res;
            }
            if (proc == null) {
                res.note = "Could not start python: '" + pythonExe + "' not found.";
                DeleteIfExists(scriptPath);
                return res;
            }
            bool finished = proc.WaitForExit(timeoutMs);
            if (!finished) {
                res.timedOut = true;
                try { proc.Kill(); } catch (Exception) { }
                DeleteIfExists(scriptPath);
                return res;
            }
            res.exitCode = proc.ExitCode;
            res.output = proc.StandardOutput.ReadToEnd();
            res.errorText = proc.StandardError.ReadToEnd();
            DeleteIfExists(scriptPath);
            return res;
        }

        static Process? StartProcess(string exe, string args) {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.Arguments = args;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            return Process.Start(psi);
        }

        static void DeleteIfExists(string path) {
            try {
                if (File.Exists(path)) File.Delete(path);
            } catch (Exception) { }
        }
    }
}
