using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FuckLogs
{
    class Credential
    {
        public string SourceFile = string.Empty;
        public string Soft = string.Empty;
        public string Host = string.Empty;
        public string Login = string.Empty;
        public string Password = string.Empty;
        public bool HasData => Login.Length > 0 || Password.Length > 0;
    }

    // Thread-safe buffered output writer. One lock per destination file so
    // different hosts/categories write in parallel; same file stays ordered.
    class BufferedFileWriter : IDisposable
    {
        private readonly object _createLock = new();
        private readonly Dictionary<string, StreamWriter> _writers = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _baseDir;

        public BufferedFileWriter(string baseDir) => _baseDir = baseDir;

        public void WriteLine(string category, string text)
        {
            var writer = GetWriter(category);
            lock (writer)
                writer.WriteLine(text);
        }

        private StreamWriter GetWriter(string category)
        {
            lock (_createLock)
            {
                if (_writers.TryGetValue(category, out var writer))
                    return writer;
                string path = Path.Combine(_baseDir, category);
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read,
                    MinerPerf.IoBufferSize, FileOptions.SequentialScan);
                writer = new StreamWriter(fs, Encoding.UTF8, MinerPerf.IoBufferSize);
                _writers[category] = writer;
                return writer;
            }
        }

        public void FlushAll()
        {
            lock (_createLock)
            {
                foreach (var writer in _writers.Values)
                {
                    lock (writer) writer.Flush();
                }
            }
        }

        public void Dispose()
        {
            lock (_createLock)
            {
                foreach (var writer in _writers.Values)
                {
                    lock (writer) writer.Dispose();
                }
                _writers.Clear();
            }
        }
    }

    // Throughput knobs for dedicated mining boxes.
    static class MinerPerf
    {
        public const int IoBufferSize = 1024 * 1024;

        public static int WorkerCount()
        {
            int cpu = Math.Max(1, Environment.ProcessorCount);
            // Mild oversubscribe so a thread waiting on disk doesn't leave a core idle.
            return cpu + Math.Max(1, cpu / 8);
        }

        public static void TuneThreadPool(int workers)
        {
            int want = Math.Min(Math.Max(workers * 2, 32), 512);
            ThreadPool.GetMinThreads(out int wt, out int ct);
            if (wt < want || ct < want)
                ThreadPool.SetMinThreads(Math.Max(wt, want), Math.Max(ct, want));
        }

        public static StreamReader OpenRead(string path)
        {
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                IoBufferSize, FileOptions.SequentialScan);
            return new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
                bufferSize: IoBufferSize, leaveOpen: false);
        }

        public static IDisposable BoostPriority() => new PriorityBoost();

        sealed class PriorityBoost : IDisposable
        {
            readonly Process _proc = Process.GetCurrentProcess();
            readonly ProcessPriorityClass _old;
            public PriorityBoost()
            {
                _old = _proc.PriorityClass;
                try { _proc.PriorityClass = ProcessPriorityClass.AboveNormal; } catch { }
            }
            public void Dispose()
            {
                try { _proc.PriorityClass = _old; } catch { }
            }
        }
    }


    // Per-target output: dedup sets + buffered writers + folder tree
    class KeywordOutput
    {
        public readonly string Root;
        public readonly string Label;
        public readonly string LogsDir;
        public readonly ConcurrentDictionary<string, byte> EmailSet = new(StringComparer.OrdinalIgnoreCase);
        public readonly ConcurrentDictionary<string, byte> UsernameSet = new(StringComparer.OrdinalIgnoreCase);
        public readonly ConcurrentDictionary<string, byte> FullLineSet = new(StringComparer.OrdinalIgnoreCase); // ULP: dedupe full url:login:pass lines
        public readonly ConcurrentDictionary<string, long> HostCounts = new(StringComparer.OrdinalIgnoreCase);
        public readonly ConcurrentDictionary<string, byte> SoftSet = new(StringComparer.OrdinalIgnoreCase);
        public readonly BufferedFileWriter EmailWriter;
        public readonly BufferedFileWriter UsernameWriter;
        public readonly BufferedFileWriter HostWriter;
        public readonly BufferedFileWriter SoftWriter;

        public KeywordOutput(string root, string label)
        {
            Root = root;
            Label = label;
            string emailsDir = Path.Combine(root, "emails");
            string usernamesDir = Path.Combine(root, "usernames");
            string byHostDir = Path.Combine(root, "by_host");
            string bySoftDir = Path.Combine(root, "by_soft");
            LogsDir = Path.Combine(root, "logs");
            Directory.CreateDirectory(emailsDir);
            Directory.CreateDirectory(usernamesDir);
            Directory.CreateDirectory(byHostDir);
            Directory.CreateDirectory(bySoftDir);
            Directory.CreateDirectory(LogsDir);
            EmailWriter = new BufferedFileWriter(emailsDir);
            UsernameWriter = new BufferedFileWriter(usernamesDir);
            HostWriter = new BufferedFileWriter(byHostDir);
            SoftWriter = new BufferedFileWriter(bySoftDir);
        }

        public void FlushAndDispose()
        {
            EmailWriter.FlushAll();
            UsernameWriter.FlushAll();
            HostWriter.FlushAll();
            SoftWriter.FlushAll();
            EmailWriter.Dispose();
            UsernameWriter.Dispose();
            HostWriter.Dispose();
            SoftWriter.Dispose();
        }
    }

    class MinerOptions
    {
        public string[] Sources = Array.Empty<string>();  // each can be a folder (scanned recursively) or an explicit file
        public string[] Keywords = Array.Empty<string>();
        public bool SplitByKeyword;
        public string OutputDir = string.Empty;   // empty = <cwd>\FuckLogs_Output
        public bool MatchLogin;                   // also match keywords against Login, not just Host
        public bool IncludePasswordOnly = true;   // keep entries that have a password but no login
        public bool ExtractEmails = true;         // keep email:password combos
        public bool ExtractUsernames = true;      // keep username:password combos
        public bool PasswordsOnly = true;         // folder scans only pick up password-list files (much faster)
    }

    class MinerProgress
    {
        public int FilesDone;
        public int FilesTotal;
        public long Creds;
        public string CurrentFile = string.Empty;
    }

    class OutputSummary
    {
        public string Label = string.Empty;
        public string Root = string.Empty;
        public int Emails;
        public int Usernames;
        public int Hosts;
        public int Software;
        public List<KeyValuePair<string, long>> TopHosts = new();
    }

    class MinerResult
    {
        public long Files;
        public long Lines;
        public long Creds;
        public long Filtered;
        public long OutputBytes;
        public string TopLevelOutput = string.Empty;
        public bool SplitByKeyword;
        public bool Cancelled;
        public TimeSpan Elapsed;
        public readonly List<OutputSummary> Outputs = new();
        public readonly List<string> Notes = new();
    }
    static class MinerEngine
    {
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Recognized log field labels (matched case-insensitively)
        private static readonly string[] SoftPrefixes = { "Soft:", "Browser:", "Application:" };
        private static readonly string[] HostPrefixes = { "Host:", "Url:" };
        private static readonly string[] LoginPrefixes = { "Login:", "Username:", "User:" };
        private static readonly string[] PasswordPrefixes = { "Password:", "Pass:" };

        public static Task<MinerResult> RunAsync(MinerOptions opt, IProgress<MinerProgress>? progress, CancellationToken ct)
            => Task.Run(() => Run(opt, progress, ct), ct);

        private static MinerResult Run(MinerOptions opt, IProgress<MinerProgress>? progress, CancellationToken ct)
        {
            var result = new MinerResult();
            var sw = Stopwatch.StartNew();
            string[] keywords = opt.Keywords;
            bool splitByKeyword = opt.SplitByKeyword && keywords.Length > 1;
            bool matchLogin = opt.MatchLogin;
            result.SplitByKeyword = splitByKeyword;
            string[] sources = opt.Sources;

            // ===== Collect files from all sources =====
            // PasswordsOnly: folder scans only pick up password-list files; explicit files always count.
            var fileList = CollectSourceFiles(sources, result.Notes,
                opt.PasswordsOnly ? LooksLikePasswordFile : null);

            int totalFiles = fileList.Count;
            result.Files = totalFiles;
            if (totalFiles == 0)
            {
                result.Notes.Add("No .txt files found.");
                sw.Stop();
                result.Elapsed = sw.Elapsed;
                return result;
            }

            // ===== Output structure =====
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseOutDir = opt.OutputDir.Length > 0 ? opt.OutputDir : Path.Combine(Directory.GetCurrentDirectory(), "FuckLogs_Output");
            var outputs = new List<KeywordOutput>();

            if (splitByKeyword)
            {
                // One independent output tree per keyword
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kw in keywords)
                {
                    string safeKw = MakeSafeFileName(kw);
                    if (safeKw.Length > 60) safeKw = safeKw.Substring(0, 60).TrimEnd('_');
                    string unique = safeKw;
                    int n = 2;
                    while (!usedNames.Add(unique)) unique = safeKw + "_" + n++;
                    var output = new KeywordOutput(Path.Combine(baseOutDir, unique, timestamp), kw);
                    outputs.Add(output);
                    result.Notes.Add($"Output [{kw}]: {output.Root}");
                }
            }
            else
            {
                string safeKeyword = keywords.Length == 0 ? "all" : MakeSafeFileName(string.Join("_", keywords));
                if (safeKeyword.Length > 60) safeKeyword = safeKeyword.Substring(0, 60).TrimEnd('_');
                var output = new KeywordOutput(Path.Combine(baseOutDir, safeKeyword, timestamp),
                    keywords.Length == 0 ? "ALL" : string.Join(", ", keywords));
                outputs.Add(output);
                result.Notes.Add($"Output: {output.Root}");
            }

            // ===== Stats =====
            long totalLines = 0, totalCreds = 0, totalFiltered = 0;
            int processedFiles = 0, lastPct = -1;
            var errors = new ConcurrentBag<(string file, string error)>();
            int maxDop = MinerPerf.WorkerCount();
            MinerPerf.TuneThreadPool(maxDop);
            result.Notes.Add($"Workers: {maxDop} (CPU {Environment.ProcessorCount})");
            var po = new ParallelOptions { MaxDegreeOfParallelism = maxDop, CancellationToken = ct };

            try
            {
                using (MinerPerf.BoostPriority())
                Parallel.ForEach(fileList, po, file =>
                {
                    long fileLines = 0;
                    try
                    {
                        using var reader = MinerPerf.OpenRead(file);
                        var current = new Credential();
                        string? line;

                        while ((line = reader.ReadLine()) != null)
                        {
                            fileLines++;
                            string t = line.Trim();
                            if (t.Length == 0) continue;

                            if (StartsWithAny(t, SoftPrefixes) is { } softVal)
                            {
                                current = new Credential { SourceFile = file, Soft = CleanSoftwareName(softVal) };
                            }
                            else if (StartsWithAny(t, HostPrefixes) is { } hostVal)
                            {
                                if (current.SourceFile.Length == 0) current.SourceFile = file;
                                current.Host = hostVal;
                            }
                            else if (StartsWithAny(t, LoginPrefixes) is { } loginVal)
                            {
                                if (current.SourceFile.Length == 0) current.SourceFile = file;
                                current.Login = loginVal;
                            }
                            else if (StartsWithAny(t, PasswordPrefixes) is { } passVal)
                            {
                                if (current.SourceFile.Length == 0) current.SourceFile = file;
                                current.Password = passVal;

                                // Keep entries with a login OR a password; password-only entries are optional
                                if (current.HasData && (opt.IncludePasswordOnly || (current.Login.Length > 0 && current.Password.Length > 0)))
                                {
                                    Interlocked.Increment(ref totalCreds);
                                    string combo = $"{current.Login}:{current.Password}";

                                    // Credential-type gate (emails vs usernames)
                                    bool isEmailLogin = IsEmail(current.Login);
                                    if ((isEmailLogin && !opt.ExtractEmails) || (!isEmailLogin && !opt.ExtractUsernames))
                                    {
                                        Interlocked.Increment(ref totalFiltered);
                                    }
                                    else if (splitByKeyword)
                                    {
                                        // Write to every keyword output this credential matches
                                        bool matchedAny = false;
                                        for (int k = 0; k < keywords.Length; k++)
                                        {
                                            if (MatchesKeyword(current, keywords[k], matchLogin))
                                            {
                                                WriteCredentialToOutput(outputs[k], current, combo);
                                                matchedAny = true;
                                            }
                                        }
                                        if (!matchedAny) Interlocked.Increment(ref totalFiltered);
                                    }
                                    else
                                    {
                                        // Combined output: keep if ANY keyword matches
                                        if (keywords.Length > 0 && !MatchesAnyKeyword(current, keywords, matchLogin))
                                            Interlocked.Increment(ref totalFiltered);
                                        else
                                            WriteCredentialToOutput(outputs[0], current, combo);
                                    }
                                }
                                current = new Credential();
                            }
                            // Lines outside entries are ignored
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add((file, ex.Message));
                    }
                    finally
                    {
                        Interlocked.Add(ref totalLines, fileLines);
                    }

                    int done = Interlocked.Increment(ref processedFiles);
                    int pct = (int)((long)done * 100 / totalFiles);
                    if (pct != lastPct || done == totalFiles)
                    {
                        lastPct = pct;
                        progress?.Report(new MinerProgress
                        {
                            FilesDone = done,
                            FilesTotal = totalFiles,
                            Creds = Interlocked.Read(ref totalCreds),
                            CurrentFile = Path.GetFileName(file)
                        });
                    }
                });
            }
            catch (OperationCanceledException)
            {
                result.Cancelled = true;
                result.Notes.Add("Cancelled by user.");
            }

            // Flush all buffered writers
            foreach (var o in outputs) o.FlushAndDispose();

            result.Lines = totalLines;
            result.Creds = totalCreds;
            result.Filtered = totalFiltered;
            result.TopLevelOutput = splitByKeyword ? baseOutDir : outputs[0].Root;
            result.OutputBytes = outputs.Sum(o => GetDirSize(o.Root));
            foreach (var o in outputs)
            {
                result.Outputs.Add(new OutputSummary
                {
                    Label = o.Label,
                    Root = o.Root,
                    Emails = o.EmailSet.Count,
                    Usernames = o.UsernameSet.Count,
                    Hosts = o.HostCounts.Count,
                    Software = o.SoftSet.Count,
                    TopHosts = o.HostCounts.OrderByDescending(kv => kv.Value).Take(5).ToList()
                });
            }

            // Write README (one per output target)
            foreach (var o in outputs)
            {
                var readmeLines = new[]
                {
                    "# FuckLogs Output",
                    "",
                    $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"Tool: FuckLogs v{Program.AppVersion}",
                    $"Sources:\n{string.Join("\n", sources.Select(s => "  - " + s))}",
                    $"Keywords: {(keywords.Length == 0 ? "ALL" : o.Label)}",
                    "",
                    "## Structure",
                    "- `emails/` - unique email:password pairs",
                    "- `usernames/` - unique username:password pairs",
                    "- `by_host/` - split by domain/host",
                    "- `by_soft/` - split by software name",
                    "- `logs/` - processing logs",
                    "",
                    $"Files: {totalFiles}, Lines: {totalLines:N0}, Creds: {totalCreds:N0}, Emails: {o.EmailSet.Count:N0}, Usernames: {o.UsernameSet.Count:N0}",
                };
                File.WriteAllLines(Path.Combine(o.Root, "README.txt"), readmeLines);
            }

            if (errors.Count > 0)
            {
                foreach (var o in outputs)
                {
                    File.WriteAllLines(Path.Combine(o.LogsDir, "errors.log"),
                        errors.Select(e => $"[{e.file}] {e.error}"));
                }
                result.Notes.Add($"{errors.Count} file(s) had errors (see logs/errors.log)");
            }

            sw.Stop();
            result.Elapsed = sw.Elapsed;
            return result;
        }

        // File-name filter for folder scans: any .txt whose name contains "password"
        // or "login" (covers passwords.txt, All_Passwords.txt, logins.txt, ...).
        private static readonly string[] PasswordFileHints = { "password", "login" };

        internal static bool LooksLikePasswordFile(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            foreach (var hint in PasswordFileHints)
                if (name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        internal static List<string> CollectSourceFiles(string[] sources, List<string> notes, Func<string, bool>? nameFilter = null)
        {
            var fileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long skipped = 0;
            foreach (var src in sources)
            {
                try
                {
                    if (File.Exists(src))
                    {
                        fileSet.Add(src);   // explicitly added file: include regardless of name
                    }
                    else if (Directory.Exists(src))
                    {
                        var enumOpts = new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true,
                            AttributesToSkip = FileAttributes.ReparsePoint
                        };
                        foreach (var f in Directory.EnumerateFiles(src, "*.txt", enumOpts))
                        {
                            if (nameFilter == null || nameFilter(f)) fileSet.Add(f);
                            else skipped++;
                        }
                    }
                    else
                    {
                        notes.Add($"Source not found: {src}");
                    }
                }
                catch (Exception ex)
                {
                    notes.Add($"Scan error [{src}]: {ex.Message}");
                }
            }
            if (nameFilter != null && skipped > 0)
                notes.Add($"Skipped {skipped:N0} non-password .txt file(s) — turn off 'Only scan password lists' in Advanced options to include them.");
            return fileSet.ToList();
        }

        static long GetDirSize(string path)
        {
            long total = 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        internal static bool ContainsIgnoreCase(string? text, string keyword)
        {
            return text != null && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string? StartsWithAny(string line, string[] prefixes)
        {
            foreach (var p in prefixes)
            {
                if (line.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return line.AsSpan(p.Length).Trim().ToString();
            }
            return null;
        }

        static string CleanSoftwareName(string soft)
        {
            // "Chrome 151.0.7922.138" -> "Chrome"
            int i = soft.Length;
            while (i > 0 && (char.IsDigit(soft[i - 1]) || soft[i - 1] == '.')) i--;
            if (i == soft.Length) return soft;
            int j = i;
            while (j > 0 && soft[j - 1] == ' ') j--;
            return j > 0 ? soft.Substring(0, j) : soft;
        }

        internal static string HostKey(string host)
        {
            // Reduce full URLs to their domain ("https://mifirm.net/register" -> "mifirm.net")
            if (host.Contains("://") && Uri.TryCreate(host, UriKind.Absolute, out var uri) && uri.Host.Length > 0)
                return uri.Host;
            return host;
        }

        internal static bool MatchesKeyword(Credential cred, string keyword, bool matchLogin)
        {
            // Keyword filter matches the Host field; optionally the Login field too
            return ContainsIgnoreCase(cred.Host, keyword) ||
                   (matchLogin && ContainsIgnoreCase(cred.Login, keyword));
        }

        internal static bool MatchesAnyKeyword(Credential cred, string[] keywords, bool matchLogin)
        {
            foreach (var kw in keywords)
                if (MatchesKeyword(cred, kw, matchLogin)) return true;
            return false;
        }

        internal static void WriteCredentialToOutput(KeywordOutput output, Credential cred, string combo)
        {
            if (IsEmail(cred.Login))
            {
                if (output.EmailSet.TryAdd(combo, 0))
                {
                    output.EmailWriter.WriteLine("emails.txt", combo);
                    if (cred.Host.Length > 0)
                    {
                        string hostKey = HostKey(cred.Host);
                        output.HostWriter.WriteLine(MakeSafeFileName(hostKey) + "_emails.txt", combo);
                        output.HostCounts.AddOrUpdate(hostKey, 1, (_, c) => c + 1);
                    }
                    if (cred.Soft.Length > 0)
                    {
                        output.SoftWriter.WriteLine(MakeSafeFileName(cred.Soft) + "_emails.txt", combo);
                        output.SoftSet.TryAdd(cred.Soft, 0);
                    }
                }
            }
            else
            {
                if (output.UsernameSet.TryAdd(combo, 0))
                {
                    output.UsernameWriter.WriteLine("usernames.txt", combo);
                    if (cred.Host.Length > 0)
                    {
                        string hostKey = HostKey(cred.Host);
                        output.HostWriter.WriteLine(MakeSafeFileName(hostKey) + "_usernames.txt", combo);
                        output.HostCounts.AddOrUpdate(hostKey, 1, (_, c) => c + 1);
                    }
                    if (cred.Soft.Length > 0)
                    {
                        output.SoftWriter.WriteLine(MakeSafeFileName(cred.Soft) + "_usernames.txt", combo);
                        output.SoftSet.TryAdd(cred.Soft, 0);
                    }
                }
            }
        }

        internal static bool IsEmail(string login)
        {
            if (string.IsNullOrWhiteSpace(login) || login.Length > 254) return false;
            return EmailRegex.IsMatch(login);
        }

        internal static string MakeSafeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }
    }

}
