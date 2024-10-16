using System;

namespace WinUpdateHelper.cfg
{
    public class Utils
    {
        public static string GetSpeedStr(float total)
        {
            string output;
            int kb_size = 1024;
            int mb_size = 1024 * 1024;

            int toMb_size = 1024 * 600;
            if (total > toMb_size)
            {
                output = string.Format("{0:F}mb/s", total / mb_size);
            }
            else
            {
                output = string.Format("{0:F}kb/s", total / kb_size);
            }

            return output;
        }
        public static string GetSizeDesc(long total)
        {
            double size_mb = Math.Round(total / 1024.0 / 1024.0, 2);
            var gOrM = "M";
            var size = size_mb;
            if (size_mb > 900f)
            {
                size = Math.Round(size_mb / 1024.0, 2);
                gOrM = "G";
            }

            return size + gOrM;
        }

        public static long GetNow()
        {
            return DateTime.Now.Ticks / 10000;
        }

        public static string ResizeString(string name, int v, string pad = " ")
        {
            var result = name;
            var len = name.Length;
            var add = v - len;
            while (add-- > 0)
            {
                result += pad;
            }

            return result;
        }
    }
}