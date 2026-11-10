using System.Diagnostics;
using PsyPath.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}

if (args.Length > 0 && args[0] == "fatal")
{
    CrashLog.Initialize(args[1], "fatal child");
    throw new InvalidOperationException("diagnostics-fatal-probe");
}

string root = Path.Combine(Path.GetTempPath(), "psypath-diagnostics-tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
// Недоступный путь не должен мешать запуску; повторная инициализация должна сработать.
string occupied = Path.Combine(root, "file");
File.WriteAllText(occupied, "not a directory");
CrashLog.Initialize(occupied, "must not throw");
CrashLog.Write("No storage", "must not throw");
CrashLog.Initialize(Path.Combine(root, "logs"), "test session");
Check(Directory.Exists(CrashLog.DirectoryPath), "fallback after inaccessible directory");

Parallel.For(0, 100, i => CrashLog.Write("Concurrent", "record-" + i));
string text = string.Join("\n", Directory.GetFiles(CrashLog.DirectoryPath).Select(File.ReadAllText));
Check(Enumerable.Range(0, 100).All(i => text.Contains(" record-" + i + "\n")), "concurrent records persist without loss");

string sensitive = "synthetic-credential-value";
CrashLog.Write("Redaction", new Exception("nested failure", new IOException("disk unavailable")),
    "password=" + sensitive + "; token=\"" + sensitive + "\"; Authorization: Bearer " + sensitive);
text = File.ReadAllText(Directory.GetFiles(CrashLog.DirectoryPath).Single());
Check(!text.Contains(sensitive) && text.Contains("[redacted]"), "credentials are redacted");
Check(text.Contains("System.IO.IOException") && text.Contains("nested failure"), "nested exception details persist");

for (int i = 0; i < 220; i++) CrashLog.Write("Rotation", new string('x', 60 * 1024));
var files = Directory.GetFiles(CrashLog.DirectoryPath);
Check(files.Length == 10 && files.All(f => new FileInfo(f).Length < 1100 * 1024), "rotation retains ten bounded files");
string? exported = CrashLog.Export(Path.Combine(root, "export"));
Check(exported != null && File.ReadAllText(exported).Contains("[Rotation]"), "export includes retained sessions");
Check(CrashLog.Export(occupied) == null, "export failure returns null without crashing");

string fatalDir = Path.Combine(root, "fatal");
var childInfo = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardError = true, RedirectStandardOutput = true };
if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
    childInfo.ArgumentList.Add(typeof(CrashLog).Assembly.Location);
childInfo.ArgumentList.Add("fatal");
childInfo.ArgumentList.Add(fatalDir);
using var child = Process.Start(childInfo)!;
await child.StandardError.ReadToEndAsync();
await child.WaitForExitAsync();
text = string.Join("\n", Directory.GetFiles(fatalDir).Select(File.ReadAllText));
Check(child.ExitCode != 0 && text.Contains("FATAL .NET") && text.Contains("diagnostics-fatal-probe") && text.Contains("Program.cs"),
    "unhandled child crash leaves exception and stack on disk");
Console.WriteLine("Diagnostics checks passed. Artifacts: " + root);
