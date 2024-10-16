using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;

namespace WinUpdateHelper
{
    [Serializable]
    public class DownloadItem : IComparable<DownloadItem>,INotifyPropertyChanged
    {
        public long total = 1;
        private long _current = 0;
        private bool _isReady = false;
        public bool isReady {
            set
            {
                _isReady = value;
                OnPropertyChanged("visibility");
            }
            get { return _isReady; }
    }
        public bool isStop { get; private set; } = false;
        internal WebResponse response;
        internal Stream stream;
        internal FileStream fs;

        public string name { get; private set; }
        public string url { get; private set; }

        public string savePath;

        public int progress
        {
            get { return (int) (_current / (1.0f * total) * 100); }
        }

        public long current
        {
            set{
                _current = value;
                OnPropertyChanged("progress");
            }
            get { return _current; }
        }

        public Visibility visibility
        {
            get { return isReady ? Visibility.Collapsed : Visibility.Visible; }
        }

        private string _desc;
        private Thread thread;

        public string desc
        {
            set
            {
                _desc = value;
                OnPropertyChanged("desc");
            }
            get { return _desc; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName="")
        {
            PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(propertyName));
        }

        public DownloadItem(string url, string name)
        {
            this.url = url;
            this.name = name;
        }

        public void Start()
        {
            if (thread != null)
            {
                thread.Abort();
            }

            thread = new Thread(_start);
            thread.Start();
        }

        private void _start()
        {
            Downloader._ThreadDownload(this);
        }

        public void Reset()
        {
            if (thread != null)
            {
                thread.Abort();
                thread = null;
            }
            this.Close();
            this.isReady = false;
            
            this.isStop = false;
        }

        internal void bind(WebResponse response, Stream stream, FileStream fs)
        {
            this.response = response;
            this.stream = stream;
            this.fs = fs;
        }

        internal void Close()
        {
            if (stream != null)
            {
                try
                {
                    stream.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                stream = null;
            }

            if (response != null)
            {
                try
                {
                    response.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                response = null;
            }

            if (fs != null)
            {
                try
                {
                    fs.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                fs = null;
            }

            isStop = true;
        }

        public int CompareTo(DownloadItem other)
        {
            if (other == null)
            {
                return 1;
            }
            var left = Regex.Match(this.name, @"\d+").Value;
            var right = Regex.Match(other.name, @"\d+").Value;
            if (left == right || left == "" || right == "")
            {
                return this.name.CompareTo(other.name);
            }
            return int.Parse(left) - int.Parse(right);
        }

        public Action<DownloadItem> readyHandle;
        public long range=0;

        internal void _InvokeReady()
        {
            var old = readyHandle;
            if (old != null)
            {
                readyHandle = null;
                old.Invoke(this);
            }
        }
    }
}