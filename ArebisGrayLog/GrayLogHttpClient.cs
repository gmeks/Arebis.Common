using Microsoft.IO;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Arebis.Logging.GrayLog
{
    /// <summary>
    /// A GrayLog client using the HTTP(S) protocol.
    /// </summary>
    public class GrayLogHttpClient : GrayLogClient
    {
        HttpClient gHttpClient;
        /// <summary>
        /// Creates a new GrayLogHttpClient using the "GrayLogFacility", "GrayLogHost" and optionally "GrayLogHttpPort" and "GrayLogHttpSecure" (true or false) AppSettings.
        /// </summary>
        public GrayLogHttpClient()
            : this(GraylogSettings.Default.GrayLogFacility, GraylogSettings.Default.GrayLogHost, GraylogSettings.Default.GrayLogHttpPort, GraylogSettings.Default.GrayLogHttpSecure)
        { }

        /// <summary>
        /// Creates a new GrayLogHttpClient.
        /// </summary>
        /// <param name="facility">Facility to set on all sent messages.</param>
        /// <param name="host">GrayLog host name.</param>
        /// <param name="port">GrayLog HTTP port.</param>
        /// <param name="useSsl">Whether to use SSL (not supported by GrayLog at this time).</param>
        public GrayLogHttpClient(string facility, string host, int port = 12201, bool useSsl = false)
            : this(facility, new Uri((useSsl ? "https://" : "http://") + host + ":" + port + "/gelf"))
        { }

        /// <summary>
        /// Creates a new GrayLogHttpClient.
        /// </summary>
        /// <param name="facility">Facility to set on all sent messages.</param>
        /// <param name="uri">GrayLog URL to send GELF messages to.</param>
        protected GrayLogHttpClient(string facility, Uri uri)
            : base(facility)
        {
            this.Uri = uri;
        }

        /// <summary>
        /// GrayLog URL to send GELF messages to.
        /// </summary>
        public Uri Uri { get; private set; }

        protected override async Task InternallySendMessageAsync(RecyclableMemoryStream messageBody)
        {
            if(gHttpClient == null)
            {
                var httpClientHandler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    UseProxy = false,                    
                };


                gHttpClient = new HttpClient(httpClientHandler);
                gHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                gHttpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            }


            HttpContent httpContent = new StreamContent(messageBody);
            HttpResponseMessage response;
            if (this.CompressionTreshold != -1 && messageBody.Length > this.CompressionTreshold)
            {
                var compressedContent = new ArebisGrayLog.CompressedContent(httpContent, "gzip");
                response = await gHttpClient.PostAsync(this.Uri, compressedContent);                
            }
            else
            {
                response = await gHttpClient.PostAsync(this.Uri, httpContent);
            }

            if (!response.IsSuccessStatusCode)
                throw new Exception("Failed to transmit log with error " + response.ReasonPhrase);
        }

        public override void Dispose()
        { }
    }
}
