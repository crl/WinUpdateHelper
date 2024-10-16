using System;
using System.Xml.Serialization;

namespace WinUpdateHelper
{
    [Serializable]
    [XmlRoot(ElementName = "info")]
    public class VersionInfoVO
    {
        public string version;
        public string uri;
    }
}