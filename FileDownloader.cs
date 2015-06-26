using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Common.Logging;
namespace TranscodeProcessor
{
    /// <summary>
    /// Handle file download/ffprobe for files provided
    /// </summary>
    public class FileDownload : Sprite.ISimpleRunnable
    {
        private static ILog log = LogManager.GetCurrentClassLogger();
        public FileDownload(string token, string url, string workingDir, string dest)
        {
            this.dest = dest;
            this.id = token;
            this.url = url;
            this.workingDir = workingDir;
            this.complete = false;
            this.failed = false;
            this.probeInfo = null;
        }
        public FileDownload(string token, string workingDir, string dest)
        {
            this.dest = dest;
            this.id = token;
            this.workingDir = workingDir;
            this.downloaded = true;
            this.probeInfo = null;
            this.failed = false;
        }
        protected bool downloaded = false;
        protected string id;
        protected string dest;
        protected string url;
        protected bool complete;
        protected string workingDir;
       
        protected bool failed;
        protected ProbeInfo probeInfo;
        public void Download()
        {
            try
            {
                if (downloaded)
                {
                    if (this.probeInfo == null)
                    {
                        var data = FFmpeg.ExecProbe(buildProbeArguments(this.workingDir + dest));

                        if (string.IsNullOrEmpty(data))
                        {
                            this.complete = true;
                            this.failed = true;
                            log.Debug("Failed to probe information from video file");
                            throw new JobFailureException("Failed to probe information from video file");
                        }
                        var serializer = new FastSerialize.Serializer(typeof(FastSerialize.JsonSerializerGeneric));

                        this.probeInfo = serializer.Deserialize<ProbeInfo>(data, false);
                    }
                    this.complete = true;
                }
                else
                {
                    //Download the file
                    System.Net.WebClient wc = new System.Net.WebClient();
                    wc.DownloadFile(url, this.workingDir + dest);
                    if (this.probeInfo == null)
                    {
                        var data = FFmpeg.ExecProbe(buildProbeArguments(this.workingDir + dest));
                        if (string.IsNullOrEmpty(data))
                        {
                            this.complete = true;
                            this.failed = true;
                            log.Debug("Failed to probe information from video file");
                            throw new JobFailureException("Failed to probe information from video file");
                        }
                        var serializer = new FastSerialize.Serializer(typeof(FastSerialize.JsonSerializerGeneric));

                        this.probeInfo = serializer.Deserialize<ProbeInfo>(data, false);
                    }

                    this.downloaded = true;
                    this.complete = true;
                }
            }
            catch (Exception ex)
            {
                log.Error("An error occured Download()", ex);
            }
        }

        public string WorkingDirectory
        {
            get { return this.workingDir; }
        }
        public ProbeInfo ProbeInfo
        {
            get { return this.probeInfo; }
        }

        public bool IsComplete
        {
            get { return this.complete; }
        }
        internal bool IsFailed
        {
            get { return this.failed; }
        }
        

        public string Destination
        {
            get
            {
                return this.dest;
            }
        }
        

        private string buildProbeArguments(string filename)
        {
            string command = "-i " + filename + " -v quiet -print_format json -show_format -show_streams";
            return command;
        }

        public override string ToString()
        {
            return "Token: " + id + " Dest: " + dest + " URL: " + url + " Complete: " + complete;
        }

        public object Handle()
        {
            Download();
            return this;
        }

        public object Handle(object resource)
        {
            throw new NotImplementedException();
        }
    }
}
