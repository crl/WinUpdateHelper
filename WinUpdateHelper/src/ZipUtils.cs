using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;
using System.Text;

namespace WinUpdateHelper
{
    public class ZipUtils
    {
        public static void UnZipFiles(string zipedFileName, string unZipDirectory, string password = "",
            bool temp = true,Action<string> logAction=null)
        {
            if (Directory.Exists(unZipDirectory) == false)
            {
                Directory.CreateDirectory(unZipDirectory);
            }

            //Android上如果不设置，解压时会报错
            Encoding utf8 = Encoding.GetEncoding("utf-8");
            ZipConstants.DefaultCodePage = utf8.CodePage;
            var zipFile = File.Open(zipedFileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);
            using (ZipInputStream zis = new ZipInputStream(zipFile))
            {
                if (!string.IsNullOrEmpty(password))
                {
                    zis.Password = password;
                }

                ZipEntry zipEntry;
                while ((zipEntry = zis.GetNextEntry()) != null)
                {
                    var path = Path.Combine(unZipDirectory, zipEntry.Name);
                    if (zipEntry.IsDirectory)
                    {
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }

                        continue;
                    }

                    if (zipEntry.IsFile)
                    {
                        var dir = Path.GetDirectoryName(path);
                        if (Directory.Exists(dir) == false)
                        {
                            Directory.CreateDirectory(dir);
                        }

                        if (temp)
                        {
                            path += ".temp";
                        }

                        if (!temp && File.Exists(path))
                        {
                            SafeFileDelete(path);
                        }

                        FileStream fs = File.Create(path);
                        if (logAction != null)
                        {
                            logAction.Invoke(path);
                        }
                        int size = 0;
                        byte[] bytes = new byte[1024 * 1024];
                        while (true)
                        {
                            size = zis.Read(bytes, 0, bytes.Length);
                            if (size > 0)
                            {
                                fs.Write(bytes, 0, size);
                            }
                            else
                            {
                                break;
                            }
                        }

                        fs.Close();
                    }

                }
            }
        }
        /// <summary>
        /// 更安全的删除文件
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool SafeFileDelete(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("tryCatch SafeFileDelete path:{0} error:{1}", path, e.Message);
                }
                return false;
            }
            return true;
        }

    }
}