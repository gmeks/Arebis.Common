using Microsoft.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace Arebis.Logging.GrayLog
{
    public class GraylogSettings
    {
        //GrayLogHttpPort = 12201
        //GrayLogUdpPort = 12201
        //GrayLogUdpMaxPacketSize ?  512

        public static RecyclableMemoryStreamManager MemoryStreamManger = new RecyclableMemoryStreamManager();

        static GraylogSettings _default;

        public static GraylogSettings Default
        {
            get
            {
                if(_default == null)
                {
                    _default = new GraylogSettings();
                    _default.GrayLogHttpPort = 12201;
                    _default.GrayLogUdpPort = 12201;
                    _default.GrayLogUdpMaxPacketSize = 512;
                }

                return _default;
            }

            set
            {
                _default = value;
            }
        }


        public int GrayLogUdpMaxPacketSize { get; set; }

        public int GrayLogHttpTimeout { get; set; }
        
        public int GrayLogHttpReadWriteTimeout { get; set; }

        public int GrayLogUdpPort { get; set; }
        
        public bool GrayLogHttpSecure { get; set; }

        public int GrayLogHttpPort { get; set; }

        public string GrayLogHost { get; set; }        

        public string GrayLogFacility { get; set; }

        public int GrayLogCompressionTreshold { get; set; }
    }
}
