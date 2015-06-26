//#define DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceProcess;
using System.Configuration;
using System.Reflection;
using System.Collections.Specialized;
using Common.Logging;

namespace TranscodeProcessor
{
    /// <summary>
    /// Service container for the application
    /// </summary>
    public class TranscoderService : ServiceBase
    {
        private static ILog log = LogManager.GetCurrentClassLogger();
        public static void Main(string[] args)
        {
            Console.WriteLine("Running Transcoder");
            if (args.Length == 0)
            {

                Console.WriteLine("Missing argument");
                Console.ReadLine();
                return;
            }
            try
            {
#if DEBUG
                var service = new TranscoderService();
                service.OnStart(args);
#else
                ServiceBase.Run(new TranscoderService());
#endif

                
            }
            catch (Exception ex)
            {
                log.Fatal("Failed to start process", ex);
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Console.WriteLine(ex.InnerException.Message);
                    Console.WriteLine(ex.InnerException.StackTrace);
                }
                //throw ex;
            }

           // System.Diagnostics.Process.GetCurrentProcess().Exited += new EventHandler(TP.TranscodeProcessor_Exited);
#if DEBUG 
            Console.ReadLine();
#endif
        }

        public TranscoderService()
        {


        }
        private bool stopped = false;
        protected AppDomain appDomain;
        protected FFRest proxy;
        protected override void OnStart(String[] args)
        {
            try
            {
                Console.WriteLine("OnStart");
                base.OnStart(args);
                // get the name of the assembly
                string exeAssembly = Assembly.GetEntryAssembly().FullName;

                // setup - there you put the path to the config file
                AppDomainSetup setup = new AppDomainSetup();
                setup.ApplicationBase = System.Environment.CurrentDirectory;
                setup.ConfigurationFile = args[0];

                // create the app domain
                appDomain = AppDomain.CreateDomain("Transcoder", null, setup);
                // create proxy used to call the startup method 
                Console.WriteLine("Initializing");
                proxy = (FFRest)appDomain.CreateInstanceAndUnwrap(
                       exeAssembly, typeof(FFRest).FullName);
                Console.WriteLine("Initialized");
                // call the startup method - something like alternative main()
                proxy.Begin();
            }
            catch (Exception ex)
            {
                log.Fatal("A fatal startup exception has occured", ex);
            }

           
        }
        protected override void OnStop()
        {
            log.Debug("OnStop Called");
            if (!stopped)
            {
                stopped = true;
                Console.WriteLine("OnStop");
                proxy.Stop();
                AppDomain.Unload(appDomain);
            }
            base.OnStop();

        }
        protected override void OnShutdown()
        {
            log.Debug("OnShutdown Called");
            if (!stopped)
            {
                stopped = true;
                proxy.Stop();
                AppDomain.Unload(appDomain);
            }
            base.OnShutdown();
        }
        new public void Dispose()
        {
            log.Debug("Dispose Called");
            if (!stopped)
            {
                stopped = true;
                proxy.Stop();
                AppDomain.Unload(appDomain);
            }
            base.OnStop();
            base.Dispose();
        }
        protected override void Dispose(bool disposing)
        {
            log.Debug("Disposing Called");
            if (!stopped)
            {
                stopped = true;
                proxy.Stop();
                AppDomain.Unload(appDomain);
            }
            base.OnStop();
            base.Dispose(disposing);
        }
    }
}
