using System;

namespace WinUpdateHelper
{
    [Serializable]
    public class InstallVersion
    {
        /// <summary>
        /// 包的标记
        /// </summary>
        public string InpakSizeFlag;

        /// <summary>
        /// 首次安装包(eg:apk)版本
        /// </summary>
        public string firstInstallVersion;

        /// <summary>
        /// 当前首包已更新的版本(用于首包缺失更新)
        /// </summary>
        public string lastPakVersion;
    }
}