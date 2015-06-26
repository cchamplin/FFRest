using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Common.Logging;

namespace TranscodeProcessor
{
    /// <summary>
    /// Contains information to be provided as a request result about the status of a transcoding
    /// task result
    /// </summary>
    public class TranscoderResult
    {
        private static ILog log = LogManager.GetCurrentClassLogger();
        public string Status
        {
            get
            {
                return status;
            }
            set
            {
                this.status = value;
            }
        }
       
        public string TaskID
        {
            get
            {
                return taskID;
            }
        }
        public int Duration
        {
            get
            {
                return (int)duration.TotalSeconds;
            }
        }
        public string JobID
        {
            get
            {
                return jobID;
            }
        }
        public int PercentComplete
        {
            get
            {
                return this.percentComplete;
            }
            set
            {
                Interlocked.CompareExchange(ref percentComplete, value, this.percentComplete); ;
            }
        }
        internal string ResultFile
        {
            get
            {
                return resultVideo;
            }
        }
        public string ResultVideo
        {
            get
            {
                if (resultVideo == null)
                {
                    return null;
                }
                if (FFRest.config["mode"] == "move")
                {
                    return FFRest.config["serve-url"] + FFRest.config["video-destination"] + "/" + this.jobID + "/" + resultVideo;
                }
                return FFRest.config["video-destination"] + "/" + resultVideo;
            }
            set
            {
                if (!File.Exists(this.workingDir + value))
                {
                    this.status = "Failed";
                    log.Error("Could not find transcoded file");
                    return;
                }
                string probeData = null;
                ProbeInfo probeInfo = null;
                try
                {
                    FileInfo fi = new FileInfo(this.workingDir + value);
                    this.fileSize = fi.Length;


                    probeData = FFmpeg.ExecProbe(buildProbeArguments(this.workingDir + value));

                    if (string.IsNullOrEmpty(probeData))
                    {
                        log.Error("Failed to grab details prob from transcoded file");
                        this.status = "Failed";
                        return;
                    }
                    var serializer = new FastSerialize.Serializer(typeof(FastSerialize.JsonSerializerGeneric));

                    probeInfo = serializer.Deserialize<ProbeInfo>(probeData, false);
                }
                catch (Exception ex)
                {
                    log.Error("Failed to grab details from transcoded file", ex);
                    this.status = "Failed";
                    this.resultVideo = value;
                }
                if (probeInfo != null)
                {
                    if (probeInfo.streams == null)
                    {
                        log.Error("Could not find transcoded file probe streams");
                        this.status = "Failed";
                    }
                    else
                    {
                        foreach (var stream in probeInfo.streams)
                        {
                            if ((stream.codec_type as string) == "video")
                            {
                                if (stream.bit_rate != null)
                                {
                                    this.bitRate = int.Parse(stream.bit_rate as string);
                                }
                                if (stream.width != null)
                                {
                                    this.width = (int)stream.width;
                                }
                                if (stream.height != null)
                                {
                                    this.height = (int)stream.height;
                                }
                            }
                        }
                    }

                    if (probeInfo.format != null)
                    {
                        if (probeInfo.format.bit_rate != null)
                        {
                            this.bitRate = int.Parse(probeInfo.format.bit_rate as string);
                        }
                        if (probeInfo.format.duration != null)
                        {
                            var dur = double.Parse(probeInfo.format.duration as string);
                            this.duration = new TimeSpan(0, 0, (int)dur);
                        }
                    }
                    else
                    {
                        log.Error("Failed to find probe format for transcoded file");
                        this.status = "Failed";
                    }
                }
                else
                {
                    log.Error("Failed to find probe data for transcoded file");
                    this.status = "Failed";
                }
                this.resultVideo = value;
            }
        }
        public int BitRate
        {
            get
            {
                return bitRate;
            }
        }
        public bool HasSegments
        {
            get
            {
                return hasSegments;
            }
        }
        public string Playlist
        {
            get
            {
                if (playList == null)
                {
                    return null;
                }
                if (FFRest.config["mode"] == "move")
                {
                    return FFRest.config["serve-url"] + FFRest.config["video-destination"] + "/" + this.jobID + "/" + playList;
                }
                return FFRest.config["video-destination"] + "/" + playList;
            }
            set
            {
                this.playList = value;
            }
        }
        internal string PlaylistFile
        {
            get
            {
                if (this.playList == null)
                {
                    return null;
                }
                return this.playList;
            }
            set
            {
                this.playList = value;
            }
        }
        internal List<string> SegmentsFiles
        {
            get
            {
                return this.segments;
            }
        }
        public string Adaptive
        {
            get
            {
                if (this.hasSegments)
                {
                    if (FFRest.config["mode"] == "move")
                    {
                        return FFRest.config["serve-url"] + FFRest.config["video-destination"] + "/" + this.jobID + "/adaptive.m3u8";
                    }
                    return FFRest.config["video-destination"] + "/adaptive.m3u8";
                }
                return null;
            }
        }
        public List<string> Segments
        {
            set
            {
                this.segments = value;
                this.hasSegments = true;
            }
        }
        public int Width
        {
            get
            {
                return width;
            }
        }
        public int Height
        {
            get
            {
                return height;
            }
        }
        public long FileSize
        {
            get
            {
                return fileSize;
            }
        }
        internal DateTime FinishTime
        {
            set
            {
                this.finishTime = value;
                this.timeProcessed = this.finishTime - this.startTime;
            }
        }
        internal DateTime StartTime
        {
            set
            {
                this.startTime = value;
            }
        }
        public TimeSpan? ProcessingTime
        {
            get {
                if (this.status == "Complete")
                {
                    return this.timeProcessed;
                }
                return null;
            }
        }
        private string status = "";
        private string taskID;
        private TimeSpan duration;
        private int bitRate;
        private DateTime startTime;
        private DateTime finishTime;
        private TimeSpan timeProcessed;
        private int width;
        private int height;
        private bool hasSegments;
        private string playList;
        private string jobID;
        private long fileSize;
        private List<string> segments;
        private volatile int percentComplete;
        private string resultVideo = null;
        private string workingDir;
        public TranscoderResult(string jobId, string taskID, string workingDirectory)
        {
            status = "Queued";
            this.jobID = jobId;
            this.taskID = taskID;
            this.percentComplete = 0;
            this.workingDir = workingDirectory;
        }

        private string buildProbeArguments(string filename)
        {
            string command = "-i " + filename + " -v quiet -print_format json -show_format -show_streams";
            return command;
        }
        public override string ToString()
        {
            return status;
        }
    }
}
