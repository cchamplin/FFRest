using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace TranscodeProcessor
{

    /// <summary>
    /// The transcode processor is the management class for the server
    /// </summary>
    public class FFRest : MarshalByRefObject
    {
        private ILog log = LogManager.GetCurrentClassLogger();
        private string host;
        private int port;
        public static Stats stats;
        public static NameValueCollection config;
        private bool stopped = false;
        private Presets presets;
        private Thread thread;
        public Server server;
        Sprite.ThreadPool transcodingPool;
        ConcurrentDictionary<string, TranscodeJob> jobs;
        private CancellationTokenSource expireCancelToken;
        bool expiring = false;
        public void Begin()
        {
            try
            {
                log.Debug("Starting processoring");
                config = ConfigurationManager.AppSettings;
                host = ConfigurationManager.AppSettings["bindhost"];
                port = int.Parse(ConfigurationManager.AppSettings["bindport"]);
                stats = new Stats();
                //BT = new BatchTranscoder();
                //LS = new ListenServer(host, port, BT);
                presets = new Presets();
                Dictionary<string, IHttpRequestHandler> handlers = new Dictionary<string, IHttpRequestHandler>();

                jobs = new ConcurrentDictionary<string, TranscodeJob>();

                expireCancelToken = new CancellationTokenSource();
                TaskExecutorFactory.Begin(ExpireJobs, 300000, 100, Timeout.Infinite, -1, expireCancelToken.Token);

                transcodingPool = new Sprite.ThreadPool(int.Parse(ConfigurationManager.AppSettings["maxtasks"]), Sprite.WaitStrategy.MODERATE);


                Directory.CreateDirectory(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["thumb-destination"]);
                Directory.CreateDirectory(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"]);


                // Initialize all the endpoint handlers
                var jobHandler = new JobHandler(jobs, transcodingPool);
                var presetHandler = new PresetHandler(presets);
                var thumbHandler = new ThumbnailHandler();
                var adaptiveHandler = new AdaptiveHandler(jobHandler.Jobs);
                var metaHandler = new MetaHandler();
                var videoHandler = new VideoHandler();
                handlers.Add("/stats", stats);
                handlers.Add("/jobs", jobHandler);
                handlers.Add("/presets", presetHandler);
                handlers.Add("/thumbs", thumbHandler);
                handlers.Add("/videos", videoHandler);
                handlers.Add("/playlist", adaptiveHandler);
                handlers.Add("/metadata", metaHandler);
                Server server = new Server(host, port, handlers);


                //server = new Httpd(5120, handlers);
                thread = new Thread(new ThreadStart(server.Listen));
                thread.Start();
                transcodingPool.StartPool();
            }
            catch (Exception ex)
            {
                log.Fatal("A fatal exception has occured", ex);
            }

        }
        private void ExpireJobs()
        {

            try
            {

                expiring = true;
                log.Trace("Running Job Expiration");
                int totalExpired = 0;
                foreach (var jobPair in jobs)
                {
                    var job = jobPair.Value;
                    if (job.CanExpire && !job.isCleaned)
                    {
                        job.CleanUp();
                        totalExpired++;

                    }
                    else if (job.CanExpire && job.isCleaned)
                    {
                        if ((DateTime.Now - job.StartTime).TotalDays > 30)
                        {
                            jobs.TryRemove(jobPair.Key, out job);
                        }
                    }
                    else if (!job.CanExpire)
                    {
                        if (job.Status.ToLower() == "waiting")
                        {
                            if ((DateTime.Now - job.StartTime).TotalHours > 10)
                            {
                                job.Complete();
                            }
                        }
                    }
                    
                }
                if (totalExpired > 0)
                {
                    log.Debug("Expired and removed " + totalExpired + " jobs ");
                }
                expiring = false;

            }
            catch (OperationCanceledException)
            {
                log.Debug("Shutting down expiration");
            }
            catch (ThreadInterruptedException)
            {
                log.Debug("Shutting down expiration");
            }
            catch (ThreadAbortException)
            {
                log.Debug("Shutting down expiration");
            }
            catch (Exception e)
            {
                log.Error("Shutting down expiration", e);
            }
            finally
            {
                expiring = false;
            }
        }
        public void Stop()
        {
            try
            {
                log.Debug("Received stop signal");
                if (!stopped)
                {
                    stopped = true;


                    presets.Shutdown();
                    //LS.ShutDown();
                    //BT.ShutDown();
                    transcodingPool.StopPool();
                    stats.Shutdown();
                    server.Shutdown();

                    expireCancelToken.Cancel(true);
                    int cnt = 0;
                    while (expiring == true && cnt < 20)
                    {
                        Thread.Sleep(15);
                        cnt++;
                    }


                    thread.Abort();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    log.Fatal("A fatal exception has occured in shutdown", ex);
                }
                catch (Exception inner)
                {
                }
            }

        }
    }
}
