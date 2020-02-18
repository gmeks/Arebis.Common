using Microsoft.IO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Arebis.Logging.GrayLog
{
    /// <summary>
    /// Base for GrayLog client implementations.
    /// </summary>
    public abstract class GrayLogClient : IGrayLogClient
    {
        /// <summary>
        /// Constructs a new GrayLogClient.
        /// </summary>
        /// <param name="facility">The facility to set on all sent messages.</param>
        protected GrayLogClient(string facility)
        {
            this.Facility = facility;
            this.CompressionTreshold = GraylogSettings.Default.GrayLogCompressionTreshold;
        }

        /// <summary>
        /// The facility to set on all sent messages.
        /// </summary>
        public string Facility { get; protected set; }

        /// <summary>
        /// Number of bytes starting at which compression is enabled (when compression is supported).
        /// -1 to disable compression completely.
        /// </summary>
        public int CompressionTreshold { get; set; }

        /// <summary>
        /// Sents a message to GrayLog.
        /// </summary>
        /// <param name="shortMessage">Short message text (required).</param>
        /// <param name="fullMessage">Full message text.</param>
        /// <param name="data">Additional details object. Can be a plain object, a string, an enumerable or a dictionary.</param>
        /// <param name="ex">An exception to log data of.</param>
        public async Task SendAsync(string shortMessage, string fullMessage = null, object data = null, Exception ex = null)
        {
            await SendAsync(shortMessage, DateTime.UtcNow, SyslogLevel.Informational, fullMessage, null, null, data, ex);
        }

        /// <summary>
        /// Sents a message to GrayLog.
        /// </summary>
        /// <param name="shortMessage">Short message text (required).</param>
        /// <param name="created">When this log line is from (required).</param>
        /// <param name="fullMessage">Full message text.</param>
        /// <param name="data">Additional details object. Can be a plain object, a string, an enumerable or a dictionary.</param>
        /// <param name="ex">An exception to log data of.</param>
        public async Task SendAsync(string shortMessage, DateTime created, SyslogLevel level = SyslogLevel.Informational, string fullMessage = null, string customerName = null, string logType = null, object data = null, Exception ex = null)
        {
            // Construct log record:
            var logRecord = new Dictionary<string, object>(14)
            {
                ["version"] = "1.1",
                ["host"] = Environment.MachineName,
                ["_facility"] = this.Facility,
                ["short_message"] = shortMessage,
                ["timestamp"] = EpochOf(created)
            };

            if (!String.IsNullOrWhiteSpace(fullMessage))
                logRecord["full_message"] = fullMessage;            

            if (data is string) logRecord["_data"] = data;
            else if (data is System.Collections.IDictionary) MergeDictionary(logRecord, (System.Collections.IDictionary)data, "_");
            else if (data is System.Collections.IEnumerable) logRecord["_values"] = data;
            else if (data != null) MergeObject(logRecord, data, "_");

            if (!logRecord.ContainsKey("Severity"))
                logRecord.Add("Severity", level.ToString());

            if (!logRecord.ContainsKey("CustomerName") && !string.IsNullOrEmpty(customerName))
                logRecord.Add("CustomerName", customerName);

            if (!logRecord.ContainsKey("LogType") && !string.IsNullOrEmpty(logType))
                logRecord.Add("LogType", logType);

            // Log exception information:
            if (ex != null)
            {
                var prefix = "";
                for (var iex = ex; iex != null; iex = iex.InnerException)
                {
                    logRecord["_ex." + prefix + "msg"] = ex.Message;
                    foreach (var key in iex.Data.Keys)
                    {
                        logRecord["_ex." + prefix + "data." + (key ?? "(null)").ToString()] = iex.Data[key];
                    }
                    prefix = "inner." + prefix;
                }
                logRecord["_ex.full"] = ex.ToString();
            }

            // Serialize object:
            var logRecordsBytes = GetJsonBytes(logRecord);
            await this.InternallySendMessageAsync(logRecordsBytes);
            logRecordsBytes?.Dispose();
        }

        RecyclableMemoryStream GetJsonBytes(Dictionary<string, object> logRecord)
        {
            string logRecordString = JsonConvert.SerializeObject(logRecord);
            ReadOnlySpan<byte> logRecordBytes = Encoding.UTF8.GetBytes(logRecordString);

            var memory = new RecyclableMemoryStream(GraylogSettings.MemoryStreamManger);
            memory.Write(logRecordBytes);
            memory.Position = 0;
            return memory;
        }

        /// <summary>
        /// Convenience method to send an exception message to GrayLog.
        /// </summary>
        /// <param name="ex">The exception to log.</param>
        /// <param name="level">The level to log the exception at.</param>
        public async Task SendAsync(Exception ex, SyslogLevel level = SyslogLevel.Error)
        {
            // Send exception:
            if (ex != null) await SendAsync(ex.Message, null, new { level = level }, ex);
        }

        /// <summary>
        /// Protocol specific implementation of (compressing and) sending of a message.
        /// </summary>
        /// <param name="uncompressedMessageBody">The uncompressed UTF8 encoded JSON message.</param>
        protected abstract Task InternallySendMessageAsync(RecyclableMemoryStream messageBody);

        /// <summary>
        /// Disposes the client.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Helper method to apply GZIP compression.
        /// </summary>
        protected RecyclableMemoryStream Compress(RecyclableMemoryStream raw, CompressionLevel compressionLevel)
        {
            raw.Position = 0;
            var memory = new RecyclableMemoryStream(GraylogSettings.MemoryStreamManger);
            using (GZipStream gzip = new GZipStream(memory, compressionLevel, true))
            {
                raw.CopyTo(gzip);
            }

            memory.Position =0;
            return memory;
        } 

        private void MergeDictionary(Dictionary<string, object> target, System.Collections.IDictionary source, string prefix)
        {
            foreach (var key in source.Keys)
            {
                target[prefix + key] = source[key];
            }
        }

        private static void MergeObject(IDictionary<string, object> target, dynamic source, string prefix = "")
        {
            foreach (PropertyInfo property in source.GetType().GetProperties())
            {
                target[prefix + property.Name] = property.GetValue(source);
            }
        }

        private static long EpochOf(DateTime dt)
        {
            TimeSpan t = dt.ToUniversalTime() - new DateTime(1970, 1, 1);
            return (long)t.TotalSeconds;
        }     
    }
}
