using SRF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Serialization;
using WinUpdateHelper.cfg;

namespace WinUpdateHelper
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private ConfigVO config;
        private ProjectVO currentProject;

        public MainWindow()
        {
            InitializeComponent();
            CompositionTarget.Rendering += Update;
            Downloader.SpeedUpdate += SpeedUpdate;

            loadXML();

        }

        private void loadXML()
        {
            var path = Path.GetFullPath("config.xml");
            if (File.Exists(path) == false)
            {
                return;
            }
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            XmlSerializer xs = new XmlSerializer(typeof(ConfigVO));
            config = (ConfigVO) xs.Deserialize(fs);
            fs.Close();

            if (string.IsNullOrEmpty(config.userName) || string.IsNullOrEmpty(config.password))
            {
                config.userName = "admin";
                config.password = "4rfv$RFV";
            }

            if (string.IsNullOrEmpty(config.updatePrefix) == false)
            {
                Updater.Prefix = config.updatePrefix;
            }

            Downloader.ThreadCount = 4;
            ServicePointManager.DefaultConnectionLimit = Downloader.ThreadCount;
            ServicePointManager.ServerCertificateValidationCallback = HttpsCallback;
            Downloader.Credential = new NetworkCredential(config.userName, config.password);

            //重导向

            var len = config.projects.Count;
            for (int i = 0; i < len; i++)
            {
                var tabItem = new TabItem();
                tabItem.Header = config.projects[i].name;
                tabItem.Width = 100;
                tabControl.Items.Add(tabItem);
            }

            var selectedIndex = 0;
            currentProject = config.projects[selectedIndex];
            tabControl.SelectedIndex = selectedIndex;
          

            this.Title = "Win更新-" + Updater.Version;
        }


        private void SpeedUpdate(int bytes)
        {
            var speedText = "";
            if (bytes > 10)
            {
                speedText = Utils.GetSpeedStr(bytes);
            }

            speedTb.Text = speedText;
        }

        private void Update(object sender, EventArgs e)
        {
            Updater.Check();
            Downloader.Update();

            if (currentProject == null || Updater.hasVersionAlert)
            {
                return;
            }

            var delta = DateTime.Now - currentProject.lastPackageCheckTime;
            var b = delta.Hours > 0.5f;
            var packageItem = currentProject.getPackageZip(b);
            
            if (packageItem != null)
            {
                if (b)
                {
                    updatePackageView(packageItem);
                }

                packageProgressBar.Value = packageItem.progress;
                if (packageItem.progress == 100)
                {
                    packageBtn.Content = "解压";
                }
            }


            var fileList = currentProject.getHpfsFile();
            if (updateBtn.Visibility != Visibility.Visible && fileList.Count>0 && currentProject.isFull==false)
            {
                foreach (var item in fileList)
                {
                    if (item.isReady == false)
                    {
                        return;
                    }
                }

                updateBtn.Visibility = Visibility.Visible;
                var result = MessageBox.Show("全部下载完成!!!");
                if (result == MessageBoxResult.OK)
                {
                    overrideHpf_info();
                    killExe(true);
                }
            }
        }

        private void killExe(bool run=false,ProjectVO project=null)
        {
            if (project == null)
            {
                project = currentProject;
            }

            var exeName = project.getExeName();
            var exePath = project.getSavePath($"win/{exeName}.exe");
            if (File.Exists(exePath))
            {
                var exe = Path.GetFileNameWithoutExtension(exePath);
                var proc = Process.GetProcessesByName(exe);
                foreach (var process in proc)
                {
                    process.Kill();
                }

                if (run)
                {
                    Process.Start(exePath);
                }
            }
        }

       
      
        
        private void overrideHpf_info()
        {
            var savePath = currentProject.getWritePath();
            var versionPath = Path.Combine(savePath, "version_install.dat");
            if (File.Exists(versionPath))
            {
                var content = File.ReadAllText(versionPath);
                var dic = Json.Deserialize(content) as Dictionary<string, object>;

                if (dic != null && dic.TryGetValue("firstInstallVersion", out object firstInstallVersion))
                {
                    var fromPath = Path.Combine(savePath, "hpf_info.json");
                    var toPath = Path.Combine(savePath, firstInstallVersion.ToString() + "_hpf_info.json");
                    if (File.Exists(fromPath) && File.Exists(toPath))
                    {
                        File.Delete(toPath);
                        File.Move(fromPath, toPath);
                    }
                }
            }
        }

        public static bool HttpsCallback(object sender, X509Certificate certificate, X509Chain chain,
            System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            clear();

            currentProject = config.projects[tabControl.SelectedIndex];

            var fileList = currentProject.getHpfsFile(true);
            if (fileList.Count > 0 && currentProject.isFull==false)
            {
                this.updateBtn.Visibility = Visibility.Visible;
            }
            var packageItem = currentProject.getPackageZip(true);
            updatePackageView(packageItem);

            listView.ItemsSource = fileList;

            this.packageCBox.IsChecked = currentProject.isLastPackageAutoDownload;
        }

        private int lastDelayKey = -1;
        private void updatePackageView(DownloadItem packageItem)
        {
            if (lastDelayKey != -1)
            {
                Delayer<object>.Stop(lastDelayKey);
                lastDelayKey = -1;
            }

            if (packageItem != null)
            {
                this.packageBtn.Visibility = Visibility.Visible;
                packageTb.Text = packageItem.desc;

                var lastFileName = currentProject.getLastDownloadPackageName();
                if (string.IsNullOrEmpty(lastFileName) == false && lastFileName == packageItem.name)
                {
                    this.packageBtn.IsEnabled = false;
                    this.packageBtn.Content = "本地一致";
                }
                else
                {
                    this.packageBtn.Content = "完整包下载";
                    this.packageBtn.IsEnabled = true;
                    lastDelayKey=Delayer<object>.Call(d => { currentProject.checkHasNewPackage(packageItem); }, null, 0.5f);
                }
            }
            else
            {
                packageTb.Text = "当前包,应该还在打包过程中,你也可以手动下载LastSuccessful";
            }

            var visibility = Visibility.Collapsed;
            var exeName = currentProject.getExeName();
            var exePath = currentProject.getSavePath($"win/{exeName}.exe");
            if (File.Exists(exePath))
            {
                visibility = Visibility.Visible;
            }

            if (this.exeBtn.Visibility != visibility)
            {
                this.clientFlagBtn.Visibility = visibility;
                this.exeBtn.Visibility = visibility;
            }
        }

        private void unZip(DownloadItem item,ProjectVO project)
        {
            this.IsEnabled = false;

            var v=new UnzipVO();
            v.item = item;
            v.project = project;
            ThreadPool.QueueUserWorkItem(_ThreadUnZip,v);
        }

        public class UnzipVO
        {
            public DownloadItem item;
            public ProjectVO project;
        }

        private void _ThreadUnZip(object o)
        {
            var v = (UnzipVO) o;
            var item = v.item;
            var project = v.project;
            var file = item.savePath;
            var name = Path.GetFileNameWithoutExtension(item.name);
            var dest = project.getSavePath(project.unzipDirName);

            ZipUtils.UnZipFiles(file, dest, "", false,zipLog);

            ///记录一下 最后下载的包
            var last=project.getSavePath("last.txt");
            File.WriteAllText(last, item.name);

            var clientFlagPath = project.getWritePath("client_flag.dat");

            var from = dest;
            if (string.IsNullOrEmpty(project.unzipDirName))
            {
                from= project.getSavePath(name);
            }

            var sb = new StringBuilder();
            if (Directory.Exists(from))
            {
                dest = project.getSavePath("win");
                if (Directory.Exists(dest))
                {
                    killExe(false, project);

                    ///把老的clientFlag记录起来 
                    if (File.Exists(clientFlagPath))
                    {
                        var content = File.ReadAllText(clientFlagPath);
                        sb.Append(content);
                    }

                    Directory.Delete(dest, true);
                }

                Directory.Move(from, dest);

                ///把老的clientFlag回复出来,强制创建一下路径
                clientFlagPath = project.getWritePath("client_flag.dat");
                if (File.Exists(clientFlagPath) == false)
                {
                    if (sb.Length == 0)
                    {
                        foreach (var str in project.getClient_flags())
                        {
                            if (string.IsNullOrWhiteSpace(str)==false)
                            {
                                sb.AppendLine(str.Trim());
                            }
                        }
                    }
                    File.WriteAllText(clientFlagPath, sb.ToString());
                }
            }

            Action a = () =>
            {
                updatePackageView(item);
                _postUnZip(file,project);
            };

            this.Dispatcher.BeginInvoke(a);
        }

        private void zipLog(string path)
        {
            Action a = () => { this.packageTb.Text = $"解压:{path}"; };
            this.Dispatcher.BeginInvoke(a);
        }

        private void _postUnZip(string file, ProjectVO project)
        {
            this.IsEnabled = true;
            killExe(true, project);

            var result = MessageBox.Show("全部下载完成!!!");
            if (result == MessageBoxResult.OK)
            {
                File.Delete(file);
            }
        }

        private void clear()
        {
            packageTb.Text = "";
            packageProgressBar.Value = 0;
            this.packageBtn.IsEnabled = true;
            this.packageBtn.Content = "完整包下载";

            this.updateBtn.Visibility = Visibility.Collapsed;
            this.packageBtn.Visibility = Visibility.Collapsed;

            var fileList = currentProject.getHpfsFile();
            foreach (var item in fileList)
            {
                item.Close();
            }
            fileList.Clear();

            var packageItem= currentProject.getPackageZip();
            if (packageItem != null)
            {
                packageItem.Close();
                packageItem = null;
            }
        }

      
        private void updateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentProject.hasExe == false)
            {
                showDownloadPack();
                return;
            }

            var fileList = currentProject.getHpfsFile();
            foreach (var item in fileList)
            {
                if (item.isReady == false)
                {
                    Downloader.Push(item);
                }
            }

            this.updateBtn.Visibility = Visibility.Collapsed;
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            clear();
        }

        private void ItemDownBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (currentProject.hasExe == false)
            {
                showDownloadPack();
                return;
            }
            var btn = sender as Button;
            var downloadItem = btn.DataContext as DownloadItem;

            Downloader.Push(downloadItem, true);
        }

        private void packageBtn_Click(object sender, RoutedEventArgs e)
        {
            var exePath = currentProject.getSavePath("win/QYN.exe");
            if (File.Exists(exePath))
            {
                var result = MessageBox.Show($"你本地已经有一个 {currentProject.name} 版本了,确定要下最新的版本???", "提示", MessageBoxButton.OKCancel);
                if (result != MessageBoxResult.OK)
                {
                    return;
                }
            }

            var packageItem = currentProject.getPackageZip();
            if (packageItem == null)
            {
                MessageBox.Show("远程还没有新包,或者网络有问题!!!");
                return;
            }

            packageBtn.IsEnabled = false;
            packageItem.readyHandle =(item)=> unZip(item, currentProject);
            Downloader.Push(packageItem);
        }

        private void clientFlagBtn_Click(object sender, RoutedEventArgs e)
        {
           
            if (currentProject.hasExe == false)
            {
                showDownloadPack();
                return;
            }

            var clientFlagPath = currentProject.getWritePath("client_flag.dat");

            if (File.Exists(clientFlagPath) == false)
            {
                var sb=new StringBuilder();
                sb.AppendLine("dir_server_url:172.30.10.207:6053");
                sb.AppendLine("enable_debug_ui:1");
                sb.AppendLine("enable_gm:1");

                File.WriteAllText(clientFlagPath,sb.ToString());
            }
            Process.Start(clientFlagPath);
        }

        private void runBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentProject.hasExe == false)
            {
                showDownloadPack();
                return;
            }

            killExe(true);
        }

       

        private void showDownloadPack()
        {
            var v = currentProject.name;
            var result = MessageBox.Show($"你本地还没有 {v} 版本呢!!!,是否要先下载个最新的 版本???", "提示", MessageBoxButton.OKCancel);
            if (result != MessageBoxResult.OK)
            {
                return;
            }

            packageBtn_Click(null, null);
        }

        private void webBtn_Click(object sender, RoutedEventArgs e)
        {
            var project = currentProject;
            var url = project.build;

            Process.Start(url);
        }

        private void localBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = currentProject.getSavePath("win");
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }
            Process.Start(path);
        }

        private void WikiBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (config != null)
            {
                Process.Start(config.wiki);
            }
        }

        private void PackageCBox_OnChecked(object sender, RoutedEventArgs e)
        {
        }

        private void PackageCBox_OnClick(object sender, RoutedEventArgs e)
        {
            var b = this.packageCBox.IsChecked==true;
            currentProject.isLastPackageAutoDownload = b;

            this.packageCBox.IsChecked = currentProject.isLastPackageAutoDownload;
        }

        private void clearCacheBtn_Click(object sender, RoutedEventArgs e)
        {
            var fileCount = 0;
            long fileSize = 0;
            var project = config.projects;
            foreach (var projectVO in project)
            {
                var dir=projectVO.getSavePath("");

                var zipFiles = Directory.GetFiles(dir, "*.zip",SearchOption.TopDirectoryOnly);
                foreach (var zipFile in zipFiles)
                {
                    var fileInfo=new FileInfo(zipFile);
                    fileSize += fileInfo.Length;
                    File.Delete(zipFile);

                    fileCount++;
                }
            }

            if (fileCount > 0)
            {
                var msg = string.Format("删除了{0}个文件,大小为:{1}", fileCount, Utils.GetSizeDesc(fileSize));
                MessageBox.Show(msg);
            }
        }
    }
}
