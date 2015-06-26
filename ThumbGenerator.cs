using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using Common.Logging;
using System.IO;
namespace TranscodeProcessor
{
   /// <summary>
   /// Handles the generation of thumbnails for a transcoding job
   /// </summary>
    public class ThumbnailGenerator : Sprite.ISimpleRunnable
    {
        
        private ILog log = LogManager.GetCurrentClassLogger();
        protected string id;
        protected string workingDir;
        protected FileDownload downloader;
        protected bool complete;
        protected string thumbTaskCommand;
        protected string taskDestination;


        public ThumbnailGenerator(string token, string workingDir, FileDownload downloader)
        {
            this.workingDir = workingDir;
            this.id = token;
            this.downloader = downloader;
            this.complete = false;
        }

        public void generate()
        {

            try
            {
                while (downloader.IsComplete == false)
                    System.Threading.Thread.Sleep(100);

                if (downloader.IsFailed)
                {
                    this.complete = true;
                    return;
                }
                try
                {
                    int duration = -1;
                    if (downloader.ProbeInfo != null && downloader.ProbeInfo.format != null)
                    {

                        if (downloader.ProbeInfo.format.duration != null)
                        {
                            var dur = double.Parse(downloader.ProbeInfo.format.duration as string);
                            duration = (int)((new TimeSpan(0, 0, (int)dur)).TotalSeconds);
                        }
                    }
                    this.thumbTaskCommand = this.buildThumbnailArguments(this.workingDir, this.downloader.Destination,duration);
                    log.Debug("Executing Thumbnail Task: " + id);
                    StringBuilder results = new StringBuilder();
                    int exitCode = 0;
                    results.Append(FFmpeg.Exec(this.thumbTaskCommand, out exitCode));
                    if (results.ToString().Contains("Output file is empty"))
                    {
                        results.Clear();
                        log.Debug("No thumbnails generated for: " + id + " trying shorter version");
                        this.thumbTaskCommand = this.buildThumbnailArgumentsShort(this.workingDir, this.downloader.Destination);
                        results.Append(FFmpeg.Exec(this.thumbTaskCommand, out exitCode));
                    }
                    log.Debug("Finished thumbnail task: " + id);
                    //TranscoderResult result = new TranscoderResult(jobID, results.ToString());
                    log.Debug("Fetched thumbnail result" + id);
                    //moveThumbs();
                    // log.Debug("Thumb files moved to remote server: " + id);  
                    if (FFRest.config["mode"] == "move")
                    {
                        
                        Directory.CreateDirectory(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["thumb-destination"] + Path.DirectorySeparatorChar + id);
                        for (var x = 1; x <= 5; x++)
                        {
                            if (File.Exists(this.workingDir + "thumbnail-" + id + "_000" + x + ".jpg"))
                            {
                                if (File.Exists(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["thumb-destination"] + Path.DirectorySeparatorChar + id + Path.DirectorySeparatorChar + "thumbnail-" + id + "_000" + x + ".jpg"))
                                {
                                    File.Delete(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["thumb-destination"] + Path.DirectorySeparatorChar + id + Path.DirectorySeparatorChar + "thumbnail-" + id + "_000" + x + ".jpg");
                                }
                                File.Move(this.workingDir + "thumbnail-" + id + "_000" + x + ".jpg", FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["thumb-destination"] + Path.DirectorySeparatorChar + id + Path.DirectorySeparatorChar + "thumbnail-" + id + "_000" + x + ".jpg");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Failed to extract thumbnails", ex);
                }

                complete = true;
            }
            catch (Exception ex)
            {
                log.Error("Exception occured before thumbnail extraction could begin", ex);
            }
        }
        public bool IsComplete
        {
            get
            {
                return complete;
            }
        }
        public override string ToString()
        {
            return "Token: " + id;// +" Dest: " + dest + " URL: " + url + " Complete: " + complete;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="workingdir"></param>
        /// <param name="filename"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        private string buildThumbnailArguments(string workingdir, string filename,int duration)
        {
            string fpsPart = "";
            if (duration > 10)
            {
                int span = duration / 5;
                fpsPart = "fps=fps=1/" + span;
            }

            string output = filename.Substring(0, filename.LastIndexOf('.'));

            string outputFile = this.workingDir + "thumbnail-" + id + "_%04d.jpg";
            string args = "";
            if (string.IsNullOrEmpty(fpsPart))
            {
                args = "-vf \"select=gt(scene\\,0.4)\" -frames:v 5 -vsync vfr" + ' ' + outputFile;
            }
            else
            {
                args = "-vf \"" + fpsPart + "\" -frames:v 5 -vsync vfr" + ' ' + outputFile;
            }
            string command = "-i " + workingdir + filename + " -y " + args;

            return command;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="workingdir"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        private string buildThumbnailArgumentsShort(string workingdir, string filename)
        {

            string output = filename.Substring(0, filename.LastIndexOf('.'));

            string outputFile = this.workingDir + "thumbnail-" + id + "_%04d.jpg";

            var args = "-vf \"select=gt(scene\\,0.05)\" -frames:v 5 -vsync vfr" + ' ' + outputFile;
            string command = "-i " + workingdir + filename + " -y " + args;

            return command;
        }

        public object Handle()
        {
            generate();
            return this;
        }

        public object Handle(object resource)
        {
            throw new NotImplementedException();
        }
    }
}
