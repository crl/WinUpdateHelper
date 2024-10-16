using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using HtmlAgilityPack;
using System.Xml.Serialization;
using System.Runtime.Remoting.Messaging;
using System.Security.Policy;
using System.Threading;

namespace WinUpdateHelper.cfg
{

    [Serializable]
    [XmlRoot(ElementName = "root")]
    public class ConfigVO
    {
        public string wiki;
        public string updateXML;
        public string updatePrefix;

        public string userName;
        public string password;

        [XmlArrayItem("item")] public List<ProjectVO> projects;
    }

    [Serializable]
    public class ProjectVO
    {
        [XmlAttribute("id")] public string id;
        [XmlAttribute("name")] public string name;
        [XmlAttribute("isFull")] public bool isFull = true;
        [XmlAttribute("exeName")] public string exeName;

        public string build;
        public string hpfs;

        public string buildReport;
        public string filters;
        public string filters_end;
        public string writePath;
        public string unzipDirName;
        public string client_flags;

        public DateTime lastPackageCheckTime;
        public bool isLastPackageAutoDownload = false;

        private List<string> _hpfs;
        private List<string> _client_flags;

        private List<string> getHpfDirs()
        {
            if (_hpfs == null)
            {
                _hpfs = hpfs.Split(',').ToList();
            }

            return _hpfs;
        }

        public List<string> getClient_flags()
        {
            if (_client_flags == null)
            {
                if (string.IsNullOrWhiteSpace(client_flags))
                {
                    client_flags = "dir_server_url:172.30.10.207:6053,enable_debug_ui:1,enable_gm:1";
                }

                _client_flags = client_flags.Trim()
                    .Split(new[] { ',', ';', '#', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            return _client_flags;
        }

        private List<DownloadItem> _hpfFiles;
        public List<DownloadItem> getHpfsFile(bool force=false)
        {
            if (_hpfFiles == null || force)
            {
                _hpfFiles = new List<DownloadItem>();
                var ftp = getHpfDirs();
                try
                {
                    foreach (var ftpFullPath in ftp)
                    {
                        var request = Downloader.GetRequest(ftpFullPath);
                        request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

                        var response = request.GetResponse();
                        var reader = new StreamReader(response.GetResponseStream());

                        var line = reader.ReadLine();

                        while (string.IsNullOrEmpty(line) == false)
                        {
                            var temp = line.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                            var name = temp[temp.Length - 1];
                            var url = ftpFullPath + "/" + name;
                            var item = new DownloadItem(url, name);
                            var total = long.Parse(temp[2]);
                            item.total = total;
                            item.desc = String.Format("{0} {1} \t{2} \t\t({3})", temp[0], temp[1],
                                Utils.ResizeString(name, 20), Utils.GetSizeDesc(total));

                            item.savePath = getWritePath(name);
                            var file = new FileInfo(item.savePath);
                            if (file.Exists)
                            {
                                item.current = file.Length;
                            }

                            _hpfFiles.Add(item);
                            line = reader.ReadLine();
                        }

                        reader.Close();
                        response.Close();
                        request = null;
                    }

                    _hpfFiles.Sort();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            return _hpfFiles;
        }

        public string getLastDownloadPackageName()
        {
            var lastFileName = string.Empty;
            var last = getSavePath("last.txt");
            if (File.Exists(last))
            {
                lastFileName = File.ReadAllText(last);
            }

            return lastFileName;
        }

        private bool hasVersionAlert = false;

        public bool checkHasNewPackage(DownloadItem packageItem)
        {
            var lastFileName = getLastDownloadPackageName();
            if (lastFileName != packageItem.name)
            {
                if (packageItem.total > 0 && packageItem.current == packageItem.total)
                {
                    //MessageBox.Show($"检测到一个新包:\n{packageZip.name}\n已下载完成,可点击解压");
                }
                else
                {
                    if (isLastPackageAutoDownload)
                    {
                        Downloader.Push(packageZip);
                    }
                    else if(hasVersionAlert==false && Updater.hasVersionAlert==false)
                    {
                        hasVersionAlert = true;
                        var result = MessageBox.Show($"检测到一个新包:\n{packageZip.name}\n是否下载它", "提示",
                            MessageBoxButton.OKCancel);
                        if (result == MessageBoxResult.OK)
                        {
                            Downloader.Push(packageZip);
                        }
                        hasVersionAlert = false;
                    }
                }

                return true;
            }

            return false;
        }

        private DownloadItem packageZip;
        public DownloadItem getPackageZip(bool force = false)
        {
            if (packageZip == null || force)
            {
                lastPackageCheckTime = DateTime.Now;

                var date = DateTime.Now.ToString("yyyyMMdd");
                var prefix = build;
                var packageName = "";
                var timeout = 1000 * 10;//10秒

                var isHtml = false;
                if (string.IsNullOrEmpty(buildReport))
                {
                    buildReport = "artifact/buildReport.txt";
                }
                else
                {
                    isHtml = true;
#if DEBUG
                    //date = "20241014";
#endif
                    buildReport = string.Format(buildReport, date);
                }


                WebRequest request = null;
                WebResponse response = null;
                StreamReader reader = null;
                var url = string.Format(prefix + "/" + buildReport);
                try
                {
                    request = (HttpWebRequest)WebRequest.Create(url);
                    request.Timeout = timeout;
                    response = request.GetResponse();
                    reader = new StreamReader(response.GetResponseStream());

                    if (isHtml)
                    {
                        packageName = getPackageNameByHtml(reader);
                        url = string.Format(prefix + "/{0}/{1}", date, packageName);
                    }
                    else
                    {
                        packageName = getPackageNameByText(reader);
                        url = string.Format(prefix + "/artifact/{0}", packageName);
                    }

                   
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return null;
                }
                finally
                {
                    reader?.Dispose();
                    response?.Close();
                    request?.Abort();
                }

                if (string.IsNullOrEmpty(packageName))
                {
                    return null;
                }


                long total;
                try
                {
                    request = (HttpWebRequest) WebRequest.Create(url);
                    request.Timeout = timeout;
                    response = request.GetResponse();
                    total = response.ContentLength;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);

                    response?.Close();
                    request?.Abort();
                    return null;
                }


                packageZip = new DownloadItem(url, packageName)
                {
                    total = total,

                    savePath = getSavePath(packageName)
                };
                var file = new FileInfo(packageZip.savePath);
                if (file.Exists)
                {
                    packageZip.current = file.Length;
                }
                packageZip.desc = String.Format("{0}\t({1})", packageName, Utils.GetSizeDesc(total));

                response.Close();
                request.Abort();

                return packageZip;
            }

            return packageZip;
        }

        public bool hasExe
        {
            get
            {
                var exeName = getExeName();
                var exePath = getSavePath($"win/{exeName}.exe");
                if (File.Exists(exePath) == false)
                {
                    return false;
                }
                return true;
            }
        }

        private string getPackageNameByText(StreamReader reader)
        {
            var packageName = string.Empty;
            while (reader.Peek() != -1)
            {
                var text = reader.ReadLine();
                var preKey = "/bin/";
                var postKey = "_Data/StreamingAssets/";
                var preIndex = text.IndexOf(preKey, StringComparison.Ordinal);
                if (preIndex == -1)
                {
                    continue;
                }

                var postIndex = text.IndexOf(postKey, StringComparison.Ordinal);
                if (postIndex == -1)
                {
                    continue;
                }

                var start = preIndex + preKey.Length;
                var end = postIndex - start;
                var simName = text.Substring(preIndex + preKey.Length, end);
                var fileNameSplit = simName.Split('/');
                var len = fileNameSplit.Length;
                if (len < 2)
                {
                    continue;
                }

                var fileName = fileNameSplit[len - 2];
                packageName = fileName + ".zip";
                break;
            }

            return packageName;
        }

        private string getPackageNameByHtml(StreamReader reader)
        {
            var result = string.Empty;
            var content=reader.ReadToEnd();

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var list = new List<string>();
            var links=doc.DocumentNode.SelectNodes("//td[@class=\"link\"]");
            foreach (var link in links)
            {
                var a = link.SelectSingleNode("a");
                if (a != null)
                {
                    var url = a.Attributes["href"].Value;

                    if (url.IndexOf(filters) != -1)
                    {
                        if (string.IsNullOrEmpty(filters_end) == false)
                        {
                            if (url.EndsWith(filters_end) == false)
                            {
                                continue;
                            }
                        }

                        list.Add(url);
                    }
                }
            }

            if (list.Count > 0)
            {
                result = list[list.Count - 1];
            }

            return result;
        }

        public string getExeName()
        {
            return exeName;
        }

        public string getSavePath(string value)
        {
            var dir = Path.GetFullPath(this.id);
            if (Directory.Exists(dir) == false)
            {
                Directory.CreateDirectory(dir);
            }
            dir=dir.Replace("\\", "/");
            if (string.IsNullOrEmpty(value))
            {
                return dir;
            }
            return dir + "/" + value;
        }

        public string getWritePath(string value="")
        {
            var dir = getSavePath(writePath);
            if (Directory.Exists(dir) == false)
            {
                Directory.CreateDirectory(dir);
            }

            if (string.IsNullOrEmpty(value))
            {
                return dir;
            }
            return dir + "/" + value;
        }
    }
}