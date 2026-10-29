using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace SephiriaExpanded
{
    internal static class AutoUpdate
    {
        internal const string ReleasesUrl = "https://github.com/psyche-kr/Sephiria-Expanded-MOD/releases";

        private const string LatestUrl = "https://api.github.com/repos/psyche-kr/Sephiria-Expanded-MOD/releases/latest";
        private const string InstalledKey = "SephiriaExpanded_UpdateInstalledTag";
        private const string LogTag = "[Sephiria Expanded][Update] ";
        private const string RunnerName = "SephiriaExpanded_UpdateRunner";
        private const string TempFolder = "update";
        private const string StageFolder = "stage";
        private const string RootFolder = "SephiriaExpanded/";
        private const string DllName = "SephiriaExpanded.dll";
        private const string MetadataName = "metadata.json";
        private const string VersionedPrefix = "SephiriaExpanded-";
        private const string OldExtension = ".old";
        private const string DigestPrefix = "sha256:";
        private const string UserAgent = "SephiriaExpanded";
        private const float StartDelay = 3f;
        private const int CheckTimeout = 15;
        private const int DownloadTimeout = 180;

        internal sealed class ReleaseAsset
        {
            public string name;
            public string browser_download_url;
            public string digest;
        }

        internal sealed class ReleaseInfo
        {
            public string tag_name;
            public bool draft;
            public bool prerelease;
            public ReleaseAsset[] assets;
        }

        private sealed class Runner : MonoBehaviour
        {
        }

        private static bool started;
        private static string installedThisSession;

        internal static string InstalledTag
        {
            get { return installedThisSession; }
        }

        private static bool IsPending(string tag)
        {
            if (string.IsNullOrEmpty(tag) || !Newer(tag, ModInfo.Version)) return false;

            string folder = ModFolder();
            string disk = folder == null ? null : ConfiguredVersion(folder);
            return disk != null && !Newer(tag, disk) && !Newer(disk, tag);
        }

        internal static void OpenHistory()
        {
            try
            {
                Application.OpenURL(ReleasesUrl);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogTag + "업데이트 내역 페이지를 열지 못했습니다: " + exception.Message);
            }
        }

        internal static void Begin()
        {
            if (started) return;

            started = true;
            GameObject holder = new GameObject(RunnerName);
            UnityEngine.Object.DontDestroyOnLoad(holder);
            holder.AddComponent<Runner>().StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(StartDelay);
            string folder = ModFolder();
            if (folder == null)
            {
                Debug.LogWarning(LogTag + "모드 폴더를 찾지 못해 업데이트를 확인하지 않습니다.");
                yield break;
            }

            Debug.Log(LogTag + "업데이트 확인 시작 (현재 " + ModInfo.Version + ")");
            string recorded = PlayerPrefs.GetString(InstalledKey, "");
            if (!string.IsNullOrEmpty(recorded))
            {
                if (IsPending(recorded))
                {
                    Debug.LogWarning(LogTag + recorded + " 을(를) 설치했지만 실행 중인 DLL은 " + ModInfo.Version + " 입니다. 릴리스에 들어 있는 DLL의 버전이 태그와 다를 수 있습니다.");
                }
                else
                {
                    PlayerPrefs.DeleteKey(InstalledKey);
                    PlayerPrefs.Save();
                }
            }

            SweepOld(folder);
            ReleaseInfo release = null;
            yield return Fetch(result => release = result);
            if (release == null) yield break;

            if (release.draft || release.prerelease || !Newer(release.tag_name, ModInfo.Version))
            {
                Debug.Log(LogTag + "최신 버전입니다. (릴리스 " + release.tag_name + ")");
                yield break;
            }

            if (PlayerPrefs.GetString(InstalledKey, "") == release.tag_name)
            {
                Debug.Log(LogTag + release.tag_name + " 은(는) 이미 받았지만 현재 버전이 " + ModInfo.Version + " 입니다. 다시 받지 않습니다.");
                yield break;
            }

            ReleaseAsset asset = FindAsset(release);
            if (asset == null)
            {
                Debug.LogWarning(LogTag + "릴리스에 zip 파일이 없습니다.");
                yield break;
            }

            Debug.Log(LogTag + "새 버전 " + release.tag_name + " 을(를) 받습니다. (현재 " + ModInfo.Version + ")");
            yield return Install(folder, release.tag_name, asset);
        }

        private static IEnumerator Fetch(Action<ReleaseInfo> result)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(LatestUrl))
            {
                request.timeout = CheckTimeout;
                request.SetRequestHeader("User-Agent", UserAgent);
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                yield return request.SendWebRequest();
                if (request.responseCode != 200 || !string.IsNullOrEmpty(request.error))
                {
                    Debug.Log(LogTag + "최신 버전을 확인하지 못했습니다. (" + request.responseCode + " " + request.error + ")");
                    yield break;
                }

                ReleaseInfo parsed = Parse(request.downloadHandler.text);
                if (parsed == null) Debug.LogWarning(LogTag + "릴리스 정보를 읽지 못했습니다.");
                else result(parsed);
            }
        }

        private static string Field(string text, string key, int start)
        {
            Match match = Regex.Match(text.Substring(start), "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static ReleaseInfo Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            ReleaseInfo release = new ReleaseInfo
            {
                tag_name = Field(text, "tag_name", 0),
                draft = Regex.IsMatch(text, "\"draft\"\\s*:\\s*true"),
                prerelease = Regex.IsMatch(text, "\"prerelease\"\\s*:\\s*true")
            };
            Match zip = Regex.Match(text, "\"name\"\\s*:\\s*\"[^\"]+\\.zip\"", RegexOptions.IgnoreCase);
            if (zip.Success)
            {
                release.assets = new[]
                {
                    new ReleaseAsset
                    {
                        name = Field(text, "name", zip.Index),
                        digest = Field(text, "digest", zip.Index),
                        browser_download_url = Field(text, "browser_download_url", zip.Index)
                    }
                };
            }

            return release;
        }

        private static ReleaseAsset FindAsset(ReleaseInfo release)
        {
            if (release.assets == null) return null;

            foreach (ReleaseAsset asset in release.assets)
            {
                if (asset != null && asset.name != null && asset.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return asset;
            }

            return null;
        }

        private static IEnumerator Install(string folder, string tag, ReleaseAsset asset)
        {
            string url = asset.browser_download_url;
            string expected = Digest(asset.digest);
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(LogTag + "내려받을 주소가 올바르지 않습니다.");
                yield break;
            }

            if (string.IsNullOrEmpty(expected))
            {
                Debug.LogWarning(LogTag + "릴리스 파일에 해시 값이 없어 받지 않습니다.");
                yield break;
            }

            string temp = Path.Combine(folder, TempFolder);
            string zip = Path.Combine(temp, VersionedPrefix + tag + ".zip");
            if (!Prepare(temp))
            {
                Debug.LogWarning(LogTag + "모드 폴더에 쓸 수 없습니다.");
                yield break;
            }

            bool downloaded = false;
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = DownloadTimeout;
                request.SetRequestHeader("User-Agent", UserAgent);
                request.downloadHandler = new DownloadHandlerFile(zip);
                yield return request.SendWebRequest();
                downloaded = request.responseCode == 200 && string.IsNullOrEmpty(request.error);
                if (!downloaded) Debug.LogWarning(LogTag + "내려받지 못했습니다. (" + request.responseCode + " " + request.error + ")");
            }

            if (downloaded && Apply(folder, temp, zip, expected, tag))
            {
                PlayerPrefs.SetString(InstalledKey, tag);
                PlayerPrefs.Save();
                installedThisSession = tag;
                Debug.Log(LogTag + tag + " 적용 완료. 게임을 다시 시작하면 새 버전이 됩니다.");
            }

            Clean(temp);
        }

        private static bool Apply(string folder, string temp, string zip, string expected, string tag)
        {
            try
            {
                if (!Verify(zip, expected))
                {
                    Debug.LogWarning(LogTag + "받은 파일의 해시가 달라 설치하지 않습니다.");
                    return false;
                }

                string stage = Path.Combine(temp, StageFolder);
                Directory.CreateDirectory(stage);
                List<string> files = Extract(zip, stage);
                if (files == null || !files.Contains(DllName) || !files.Contains(MetadataName))
                {
                    Debug.LogWarning(LogTag + "압축 파일의 구성이 올바르지 않아 설치하지 않습니다.");
                    return false;
                }

                return Replace(folder, stage, files, tag);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogTag + "설치 중 문제가 생겼습니다: " + exception.Message);
                return false;
            }
        }

        private static List<string> Extract(string zip, string stage)
        {
            string stageRoot = Path.GetFullPath(stage) + Path.DirectorySeparatorChar;
            List<string> files = new List<string>();
            using (ZipArchive archive = ZipFile.OpenRead(zip))
            {
                string prefix = RootFolder;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    if (name.EndsWith("/", StringComparison.Ordinal)) continue;

                    if (!name.StartsWith(RootFolder, StringComparison.Ordinal)) prefix = "";
                }

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    if (name.EndsWith("/", StringComparison.Ordinal)) continue;

                    name = name.Substring(prefix.Length);
                    if (name.Length == 0 || name.StartsWith("/", StringComparison.Ordinal) || name.Contains("..") || Path.IsPathRooted(name)) return null;

                    string target = Path.GetFullPath(Path.Combine(stage, name));
                    if (!target.StartsWith(stageRoot, StringComparison.OrdinalIgnoreCase)) return null;

                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    entry.ExtractToFile(target, true);
                    files.Add(name);
                }
            }

            return files;
        }

        private static bool Replace(string folder, string stage, List<string> files, string tag)
        {
            List<string[]> done = new List<string[]>();
            string currentDll = CurrentFile();
            string versioned = null;
            try
            {
                foreach (string relative in files)
                {
                    string source = Path.Combine(stage, relative);
                    string destination = Path.Combine(folder, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    string backup = null;
                    if (File.Exists(destination))
                    {
                        backup = destination + "." + DateTime.Now.Ticks + OldExtension;
                        try
                        {
                            File.Move(destination, backup);
                        }
                        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                        {
                            if (!string.Equals(Path.GetFileName(destination), currentDll, StringComparison.OrdinalIgnoreCase)) throw;

                            Debug.LogWarning(LogTag + "실행 중인 DLL의 이름을 바꾸지 못해 버전이 붙은 새 파일로 설치합니다.");
                            versioned = VersionedPrefix + tag + ".dll";
                            string versionedPath = Path.Combine(folder, versioned);
                            done.Add(new[] { versionedPath, null });
                            File.Copy(source, versionedPath, true);
                            continue;
                        }
                    }

                    done.Add(new[] { destination, backup });
                    File.Copy(source, destination, true);
                }

                if (versioned != null) SetMetadataDll(Path.Combine(folder, MetadataName), versioned);

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogTag + "파일 교체에 실패해 되돌립니다: " + exception.Message);
                Rollback(done);
                return false;
            }
        }

        private static void Rollback(List<string[]> done)
        {
            for (int i = done.Count - 1; i >= 0; i--)
            {
                string destination = done[i][0];
                string backup = done[i][1];
                try
                {
                    if (File.Exists(destination)) File.Delete(destination);

                    if (backup != null && File.Exists(backup)) File.Move(backup, destination);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(LogTag + "되돌리기 실패: " + destination + " (" + exception.Message + ")");
                }
            }
        }

        private static void SetMetadataDll(string path, string dllFile)
        {
            string text = File.ReadAllText(path);
            string replaced = Regex.Replace(text, "\"dllFile\"\\s*:\\s*\"[^\"]*\"", "\"dllFile\":  \"" + dllFile + "\"");
            if (replaced == text) throw new IOException("metadata.json 의 dllFile 항목을 찾지 못했습니다.");

            File.WriteAllText(path, replaced);
        }

        private static void SweepOld(string folder)
        {
            try
            {
                foreach (string path in Directory.GetFiles(folder, "*" + OldExtension, SearchOption.AllDirectories))
                    TryDelete(path);

                string current = CurrentFile();
                string configured = ConfiguredDll(folder);
                foreach (string path in Directory.GetFiles(folder, VersionedPrefix + "*.dll", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(path);
                    if (!string.Equals(name, current, StringComparison.OrdinalIgnoreCase) && !string.Equals(name, configured, StringComparison.OrdinalIgnoreCase)) TryDelete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogTag + "이전 파일을 정리하지 못했습니다: " + exception.Message);
            }
        }

        private static string ConfiguredVersion(string folder)
        {
            try
            {
                Match match = Regex.Match(File.ReadAllText(Path.Combine(folder, MetadataName)), "\"modVersion\"\\s*:\\s*\"([^\"]*)\"");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch
            {
                return null;
            }
        }

        private static string ConfiguredDll(string folder)
        {
            try
            {
                Match match = Regex.Match(File.ReadAllText(Path.Combine(folder, MetadataName)), "\"dllFile\"\\s*:\\s*\"([^\"]*)\"");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch
            {
                return null;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }

        private static bool Prepare(string temp)
        {
            try
            {
                Clean(temp);
                Directory.CreateDirectory(temp);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void Clean(string temp)
        {
            try
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
            catch
            {
            }
        }

        private static string Digest(string digest)
        {
            if (string.IsNullOrEmpty(digest) || !digest.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase)) return null;

            return digest.Substring(DigestPrefix.Length).Trim().ToLowerInvariant();
        }

        private static bool Verify(string path, string expected)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == expected;
        }

        private static string ModFolder()
        {
            try
            {
                string location = Assembly.GetExecutingAssembly().Location;
                return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
            }
            catch
            {
                return null;
            }
        }

        private static string CurrentFile()
        {
            try
            {
                string location = Assembly.GetExecutingAssembly().Location;
                return string.IsNullOrEmpty(location) ? null : Path.GetFileName(location);
            }
            catch
            {
                return null;
            }
        }

        private static bool Newer(string latest, string current)
        {
            int[] a = Parts(latest);
            int[] b = Parts(current);
            if (a == null || b == null) return false;

            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0;
                int y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }

            return false;
        }

        private static int[] Parts(string version)
        {
            if (string.IsNullOrEmpty(version)) return null;

            Match match = Regex.Match(version, "\\d+(\\.\\d+)*");
            if (!match.Success) return null;

            string[] pieces = match.Value.Split('.');
            int[] parts = new int[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                if (!int.TryParse(pieces[i], out parts[i])) return null;
            }

            return parts;
        }
    }
}
