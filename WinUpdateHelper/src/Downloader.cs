using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace WinUpdateHelper
{
    public class Downloader
    {
        public static int ThreadCount = 4;

        public static NetworkCredential Credential;
        public static Action<int> SpeedUpdate;
        private static int buffSize = 1024 * 1024 * 8;

        private static List<DownloadItem> queueTaskList = new List<DownloadItem>();
        private static List<DownloadItem> loadingList = new List<DownloadItem>();


        private static int SecondDownloadSize=0;
        public static WebRequest GetRequest(string ftpFullPath)
        {
            var request = WebRequest.Create(ftpFullPath);

            if (request is FtpWebRequest ftpRequest)
            {
                if (Credential != null)
                {
                    ftpRequest.Credentials = Credential;
                }

                ftpRequest.UseBinary = true;
                ftpRequest.UsePassive = true;
                ftpRequest.KeepAlive = true;
            }

            return request;
        }



        
        public static void Push(DownloadItem download, bool isShfit = false)
        {
            if (download == null || loadingList.Contains(download))
            {
                return;
            }

            queueTaskList.Remove(download);
            download.Reset();

            if (isShfit)
            {
                queueTaskList.Insert(0, download);
            }
            else
            {
                queueTaskList.Add(download);
            }
        }

        internal static void _ThreadDownload(DownloadItem item)
        {
            if (item == null || item.isStop)
            {
                return;
            }

            WebRequest request = null;
            WebResponse response = null;
            Stream stream = null;
            try
            {
                var dir = Path.GetDirectoryName(item.savePath);
                if (Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }

                FileStream writer = new FileStream(item.savePath, FileMode.OpenOrCreate, FileAccess.Write,
                    FileShare.ReadWrite);

                long startOffset = 0;
                request = GetRequest(item.url);
                if (request is FtpWebRequest ftpWebRequest)
                {
                    request.Method = WebRequestMethods.Ftp.DownloadFile;

                }
                else if (request is HttpWebRequest httpWebRequest)
                {
                    startOffset = writer.Length;
                    if (writer.Length > item.total)
                    {
                        writer.Close();
                        File.Delete(item.savePath);
                        writer = new FileStream(item.savePath, FileMode.OpenOrCreate, FileAccess.Write,
                            FileShare.ReadWrite);
                        startOffset = 0;
                    }
                    else if (startOffset == item.total)
                    {
                        item.current = startOffset;
                        writer.Close();
                        request.Abort();
                        item.Close();

                        item.isReady = true;
                        lock (readyQueue)
                        {
                            readyQueue.Enqueue(item);
                        }

                        return;
                    }

                    httpWebRequest.AddRange(startOffset);
                    writer.Seek(startOffset, SeekOrigin.Begin); //指针跳转
                }

                item.current = startOffset;
                var buff = new byte[buffSize];
                response = request.GetResponse();
                stream = response.GetResponseStream();

                item.bind(response, stream, writer);

                int size = stream.Read(buff, 0, buffSize);

                while (size > 0 && item.isStop == false)
                {
                    SecondDownloadSize += size;
                    item.current += size;
                    writer.Write(buff, 0, size);
                    writer.Flush();
                    size = stream.Read(buff, 0, buffSize);
                }

                stream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                try
                {
                    request?.Abort();
                }
                catch (Exception)
                {
                }
            }

            if (item.isStop == false)
            {
                item.isReady = true;
            }

            item.Close();

            if (item.isReady)
            {
                lock (readyQueue)
                {
                    readyQueue.Enqueue(item);
                }
            }
        }

        private static Queue<DownloadItem> readyQueue=new Queue<DownloadItem>();
        private static long lastTime = 0;

        internal static void Update()
        {
            if (GetNow() - lastTime > 1000)
            {
                SpeedUpdate.Invoke(SecondDownloadSize);
                SecondDownloadSize = 0;
                lastTime = GetNow();
            }


            lock (readyQueue)
            {
                while (readyQueue.Count > 0)
                {
                    var item = readyQueue.Dequeue();
                    if (item.isReady)
                    {
                        item._InvokeReady();
                    }
                }
            }

            while (queueTaskList.Count > 0 && loadingList.Count < ThreadCount)
            {
                var item = queueTaskList[0];
                queueTaskList.RemoveAt(0);

                if (item.isStop == false)
                {
                    loadingList.Add(item);
                    item.Start();
                }
            }

            for (int i = loadingList.Count - 1; i > -1; i--)
            {
                var item = loadingList[i];
                if (item.isReady || item.isStop)
                {
                    loadingList.RemoveAt(i);
                }
            }

        }


        private static long GetNow()
        {
            return DateTime.Now.Ticks / 10000;
        }
    }
}