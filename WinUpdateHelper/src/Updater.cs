using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using System.Xml.Serialization;

namespace WinUpdateHelper
{
    public class Updater
    {
        private static DateTime lastDateTime;

        /// <summary>
        /// 几秒钟检测一次
        /// </summary>
        public static int limitCheckSecond=60;
        private static DateTime appStartTime=DateTime.Now;
        public static string Prefix = "http://sh-chenrl.snda.root.corp/doc/tools";
        private static string Temp = "temp";
        private static string TempAppDir = "tempApp";
        private static bool isFirst = true;


        public static string Version
        {
            get
            {
                var assemblyName = Application.ResourceAssembly.GetName();
                return assemblyName.Version.ToString();
            }
        }

        public static bool hasVersionAlert
        {
            get;
            private set;
        } = false;

        public static void Check(string appName = null)
        {
            var now = DateTime.Now;

            var delta = now - appStartTime;


            if (delta.TotalSeconds > 0.3f && isFirst)
            {
                isFirst = false;
                var exePath = Process.GetCurrentProcess().MainModule.FileName;
                var path = Path.GetDirectoryName(exePath);
                var currentDirInfo = new DirectoryInfo(path);
                //MessageBox.Show(currentDirInfo.Name, "t", MessageBoxButton.OK);
                if (currentDirInfo.Name == TempAppDir)
                {
                    var appDirInfo = currentDirInfo.Parent.Parent;

                    foreach (FileInfo fileInfo in currentDirInfo.GetFiles("*.*", SearchOption.AllDirectories))
                    {
                        var sourcePath = fileInfo.FullName.Replace("\\", "/");
                        var targetPath = sourcePath.Replace($"{Temp}/{TempAppDir}/", "");
                        var targetDir = Path.GetDirectoryName(targetPath);
                        if (Directory.Exists(targetDir) == false)
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        File.Copy(sourcePath, targetPath, true);
                    }

                    var exeName = Path.GetFileName(exePath);
                    exePath = Path.Combine(appDirInfo.FullName, exeName);
                    Process.Start(exePath);

                    Process.GetCurrentProcess().Kill();
                    return;
                }
            }


            if (lastDateTime != null)
            {
                delta = now - lastDateTime;
                if (delta.TotalSeconds < limitCheckSecond)
                {
                    return;
                }
            }


            lastDateTime = now;

            var assemblyName = Application.ResourceAssembly.GetName();

            if (string.IsNullOrEmpty(appName))
            {
                appName = assemblyName.Name;
            }

            var uri = $"{Prefix}/update_{appName}.xml";

            try
            {
                var webClient = new WebClient();
                var stream = webClient.OpenRead(uri);
                var reader = new StreamReader(stream);
                var xmlStr = reader.ReadToEnd();

                VersionInfoVO versionInfoVO;
                var xs = new XmlSerializer(typeof(VersionInfoVO));
                var newVersion = 0;
                using (var strReader = new StringReader(xmlStr))
                {
                    versionInfoVO = (VersionInfoVO)xs.Deserialize(strReader);

                    if (versionInfoVO.uri.StartsWith("http") == false)
                    {
                        versionInfoVO.uri = Prefix + "/" + versionInfoVO.uri;
                    }
                }

                newVersion = GetVersionCount(versionInfoVO.version);

                var versionInfo = assemblyName.Version.ToString();

                var currentVersion = GetVersionCount(versionInfo);

                if (newVersion > currentVersion && hasVersionAlert == false)
                {
                    hasVersionAlert = true;
                    var result = MessageBox.Show($"发现新版本:{versionInfoVO.version},是否更新?", "更新", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        var name = Path.GetFileName(versionInfoVO.uri);
                        var downloadItem = new DownloadItem(versionInfoVO.uri, name);

                        var path = Path.GetFullPath(Temp);
                        if (Directory.Exists(path) == false)
                        {
                            Directory.CreateDirectory(path);
                        }

                        downloadItem.savePath = Path.Combine(path, name);
                        downloadItem.readyHandle += unZip;
                        downloadItem.Start();
                    }

                    hasVersionAlert = false;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK);
            }
        }

        private static void unZip(DownloadItem item)
        {
            var file = item.savePath;
            var name = Path.GetFileNameWithoutExtension(item.name);

            var dest = Path.GetFullPath($"{Temp}/{TempAppDir}");
            ZipUtils.UnZipFiles(file, dest, "", false);

            var exe = Path.Combine(dest, name+".exe");
            Process.Start(exe);

            Process.GetCurrentProcess().Kill();
        }

        private static int GetVersionCount(string version)
        {
            var vs = version.Split('.');
            int versionCount = 0;
            for (int i = 0, len = vs.Length; i < len; i++)
            {
                versionCount += int.Parse(vs[len - 1 - i]) * (int) Math.Pow(10, i * 2);
            }

            return versionCount;
        }
    }
}