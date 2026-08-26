using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FuckLogs
{
    class UlpOptions
    {
        public string[] Sources = Array.Empty<string>();  // each can be a folder (scanned recursively) or an explicit file
        public string[] Keywords = Array.Empty<string>();
        public bool SplitByKeyword;
        public string OutputDir = string.Empty;   // empty = <cwd>\FuckLogs_Output
        public bool MatchLogin;                   // also match keywords against Login, not just Host
        public bool ExtractEmails = true;         // keep combos whose login is an email
        public bool ExtractUsernames = true;      // keep combos whose login is a plain username
    }

    // ULP = Url Login Password, one per line:
    //   https://spytrac.com/:nyongesamalan@gmail.com:malan92
    //   nativecamp.net/recruit/login:paulwilliams65650@gmail.com:malan001
    //   login.ashford.edu/:KIjone4546:Agent218
    static class UlpEngine
    {
        public static Task<MinerResult> RunAsync(UlpOptions opt, IProgress<MinerProgress>? progress, CancellationToken ct)
            => Task.Run(() => Run(opt, progress, ct), ct);

        private static MinerResult Run(UlpOptions opt, IProgress<MinerProgress>? progress, CancellationToken ct)
        {
            var result = new MinerResult();
            var sw = Stopwatch.StartNew();
            string[] keywords = opt.Keywords;
            bool splitByKeyword = opt.SplitByKeyword && keywords.Length > 1;
            bool matchLogin = opt.MatchLogin;
            result.SplitByKeyword = splitByKeyword;
            string[] sources = opt.Sources;

            // ===== Collect files from all sources =====
            var fileList = MinerEngine.CollectSourceFiles(sources, result.Notes);

            int totalFiles = fileList.Count;
            result.Files = totalFiles;
            if (totalFiles == 0)
            {
                result.Notes.Add("No .txt files found.");
                sw.Stop();
                result.Elapsed = sw.Elapsed;
                return result;
            }

            // ===== Output structure (same tree as the miner) =====
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseOutDir = opt.OutputDir.Length > 0 ? opt.OutputDir : Path.Combine(Directory.GetCurrentDirectory(), "FuckLogs_Output");
            var outputs = new List<KeywordOutput>();

            if (splitByKeyword)
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kw in keywords)
                {
                    string safeKw = MinerEngine.MakeSafeFileName(kw);
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
                string safeKeyword = keywords.Length == 0 ? "all" : MinerEngine.MakeSafeFileName(string.Join("_", keywords));
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
                        string? line;

                        while ((line = reader.ReadLine()) != null)
                        {
                            fileLines++;
                            string t = line.Trim();
                            if (t.Length == 0 || t.StartsWith("#")) continue;

                            if (!TryParseUlp(t, out string url, out string login, out string password)) continue;

                            Interlocked.Increment(ref totalCreds);
                            var cred = new Credential { SourceFile = file, Host = HostFromUrl(url), Login = login, Password = password };

                            // Credential-type gate (email logins vs plain usernames)
                            bool isEmailLogin = MinerEngine.IsEmail(login);
                            if ((isEmailLogin && !opt.ExtractEmails) || (!isEmailLogin && !opt.ExtractUsernames))
                            {
                                Interlocked.Increment(ref totalFiltered);
                            }
                            else if (splitByKeyword)
                            {
                                bool matchedAny = false;
                                for (int k = 0; k < keywords.Length; k++)
                                {
                                    if (MinerEngine.MatchesKeyword(cred, keywords[k], matchLogin))
                                    {
                                        WriteUlpToOutput(outputs[k], cred, t);
                                        matchedAny = true;
                                    }
                                }
                                if (!matchedAny) Interlocked.Increment(ref totalFiltered);
                            }
                            else
                            {
                                if (keywords.Length > 0 && !MinerEngine.MatchesAnyKeyword(cred, keywords, matchLogin))
                                    Interlocked.Increment(ref totalFiltered);
                                else
                                    WriteUlpToOutput(outputs[0], cred, t);
                            }
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

            foreach (var o in outputs)
            {
                var readmeLines = new[]
                {
                    "# FuckLogs ULP Output",
                    "",
                    $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"Tool: FuckLogs v{Program.AppVersion}",
                    $"Sources:\n{string.Join("\n", sources.Select(s => "  - " + s))}",
                    $"Keywords: {(keywords.Length == 0 ? "ALL" : o.Label)}",
                    "",
                    "## Structure",
                    "- `emails/` - unique email:password combos",
                    "- `usernames/` - unique username:password combos",
                    "- `by_host/` - full url:login:password lines split by domain/host",
                    "- `logs/` - processing logs",
                    "",
                    $"Files: {totalFiles}, Lines: {totalLines:N0}, ULPs: {totalCreds:N0}, Emails: {o.EmailSet.Count:N0}, Usernames: {o.UsernameSet.Count:N0}",
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

        // Writes a parsed ULP entry:
        //   emails/ + usernames/  -> bare "login:password" combos (deduped per combo)
        //   by_host/              -> full "url:login:password" lines (deduped per line)
        static void WriteUlpToOutput(KeywordOutput output, Credential cred, string fullLine)
        {
            string loginCombo = $"{cred.Login}:{cred.Password}";
            if (MinerEngine.IsEmail(cred.Login))
            {
                if (output.EmailSet.TryAdd(loginCombo, 0))
                    output.EmailWriter.WriteLine("emails.txt", loginCombo);
            }
            else
            {
                if (output.UsernameSet.TryAdd(loginCombo, 0))
                    output.UsernameWriter.WriteLine("usernames.txt", loginCombo);
            }

            if (cred.Host.Length > 0 && output.FullLineSet.TryAdd(fullLine, 0))
            {
                string hostKey = MinerEngine.HostKey(cred.Host);
                output.HostWriter.WriteLine(MinerEngine.MakeSafeFileName(hostKey) + ".txt", fullLine);
                output.HostCounts.AddOrUpdate(hostKey, 1, (_, c) => c + 1);
            }
        }

        // Splits "url:login:password". The URL itself may contain ':' (scheme "https://",
        // ports ":8080/"), so the separator is the first ':' that is neither part of "://"
        // nor a port number. The password keeps any remaining ':' characters.
        internal static bool TryParseUlp(string line, out string url, out string login, out string password)
        {
            url = login = password = string.Empty;
            int sep = FindUrlSeparator(line);
            if (sep <= 0) return false;
            url = line.Substring(0, sep).Trim();
            string rest = line.Substring(sep + 1);
            int colon = rest.IndexOf(':');
            if (colon <= 0 || colon >= rest.Length - 1) return false;
            login = rest.Substring(0, colon).Trim();
            password = rest.Substring(colon + 1);
            return url.Length > 0 && login.Length > 0 && password.Length > 0;
        }

        static int FindUrlSeparator(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != ':') continue;

                // scheme separator "://" (e.g. https://, android://)
                if (i + 1 < line.Length && line[i + 1] == '/') continue;

                // port number ":8080" followed by a path or the credentials separator
                int j = i + 1;
                while (j < line.Length && char.IsDigit(line[j])) j++;
                if (j > i + 1 && j < line.Length && (line[j] == '/' || line[j] == ':'))
                {
                    i = j - 1; // resume scanning at the delimiter after the port
                    continue;
                }

                return i;
            }
            return -1;
        }

        // "https://user@host.com:8080/path" -> "host.com"
        internal static string HostFromUrl(string url)
        {
            string h = url;
            int scheme = h.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0) h = h.Substring(scheme + 3);
            int slash = h.IndexOf('/');
            if (slash >= 0) h = h.Substring(0, slash);
            int colon = h.IndexOf(':');
            if (colon >= 0) h = h.Substring(0, colon);
            int at = h.LastIndexOf('@');
            if (at >= 0) h = h.Substring(at + 1);
            return h;
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
    }
}

