/*   Copyright 2016 Cinegy Gmbh  */

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Threading;
using System.Xml.Linq;
using CommandLine;
using static System.String;

namespace AirFailoverWatchdog
{
    internal class Program
    {
        private static readonly Options Options = new Options();
        private static DateTime _startTime = DateTime.Now;
        private static bool _pendingExit;
        private static ServiceHost _serviceHost;
        private static AirFailoverWatchdogApi _airFailoverWatchdogApi;
        private static List<PlayoutEngine> _monitoredEngines = new List<PlayoutEngine>();
        private static PlayoutEngine _backupEngine = new PlayoutEngine();
        private static PlayoutEngine _lastEngineLoadedOntoBackup;
        private static List<Thread> _monitoringThreads = new List<Thread>();
        private static TimeSpan _airTimeout = new TimeSpan(0, 0, 5);
        private static TimeSpan _airRecoveryTime = new TimeSpan(0,0,10);

        private static PlayoutEngine _currentFailedOverEngine = null;

        static void Main(string[] args)
        {

            Console.CancelKeyPress += Console_CancelKeyPress;

            Console.WriteLine("Cinegy Air Watchdog Failover Tool v1.0.0 ({0})\n",
                File.GetCreationTime(Assembly.GetExecutingAssembly().Location));

            try
            {
                Console.SetWindowSize(100, 40);
            }
            catch
            {
                Console.WriteLine("Failed to increase console size - probably screen resolution is low");
            }

            SetupApplication();
            
            MainLoop();

            LogMessage("Application terminated on request - press enter to close");
            Console.ReadLine();
            Environment.Exit(-1);
        }

        private static void SetupApplication()
        {
            Console.Clear();

            LoadConfig();

            if (!IsNullOrWhiteSpace(Options.LogFile))
            {
                LogMessage("Logging events to file {0}", Options.LogFile);
            }
            LogMessage("Logging started.");

            //for each air server to be watched, spin off a monitoring thread to check continuous multicast input

            foreach (var engine in _monitoredEngines)
            {
                LogMessage("Monitoring engine {0} on address {1}, port {2}", engine.Name, engine.multicastAddress, engine.multicastGroupPort);

                var ts = new ThreadStart(delegate
                {
                    EngineMonitoringThreadWorker(engine);
                });

                var engineMonitoringThread = new Thread(ts) { Priority = ThreadPriority.AboveNormal };

                engineMonitoringThread.Start();

                _monitoringThreads.Add(engineMonitoringThread);
            }
        
            //todo: review web services now the model is a little more developed, and activate

            //if (Options.EnableWebServices)
            //{
            //    var httpThreadStart = new ThreadStart(delegate
            //    {
            //        StartHttpService(Options.ServiceUrl);
            //    });

            //    var httpThread = new Thread(httpThreadStart) { Priority = ThreadPriority.Normal };

            //    httpThread.Start();
            //}


        }

        private static void EngineMonitoringThreadWorker(PlayoutEngine engine)
        {
            var listenAdapter = Options.AdapterAddress;

            //   private static void StartListeningToNetwork(string multicastAddress, int multicastGroup, string listenAdapter = "")
            var listenAddress = IsNullOrEmpty(listenAdapter) ? IPAddress.Any : IPAddress.Parse(listenAdapter);

            var localEp = new IPEndPoint(listenAddress, engine.multicastGroupPort);


            var _udpClient = new UdpClient { ExclusiveAddressUse = false };
            using (_udpClient)
            {
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.ReceiveBufferSize = 1024 * 256;
                _udpClient.Client.Bind(localEp);
               
                var parsedMcastAddr = IPAddress.Parse(engine.multicastAddress);
                _udpClient.JoinMulticastGroup(parsedMcastAddr);

                while (true)
                {
                    var data = _udpClient.Receive(ref localEp);
                 
                    if (data == null) continue;
                    try
                    {
                        engine.lastMonitoredPacketTime = DateTime.Now;
                        engine.networkFailureCount = 0;
                    }
                    catch (Exception ex)
                    {
                        LogMessage(
                            $@"Network problem with engine {engine.Name} - retrying (attempt {
                                engine.networkFailureCount++})");

                        if (engine.networkFailureCount < 5)
                        {
                            Thread.Sleep(1000);
                            try
                            {
                                _udpClient.Client.Close();
                                _udpClient.Close();
                            }
                            catch
                            {
                                //above was just housekeeping, should not matter if it fails
                            }

                            //call recursively to set up again, see if it is all happy now.
                            EngineMonitoringThreadWorker(engine);
                        }
                        else
                        {
                            LogMessage($@"Too many network failures for engine {engine.Name}");
                            throw new Exception($@"Engine network monitoring failure - inner exception: {ex.Message}");
                        }
                    }
                }
            }
        }

        private static void MainLoop()
        {
            while (!_pendingExit)
            {
                var runningTime = DateTime.Now.Subtract(_startTime);

                Console.Clear();

                Console.SetCursorPosition(0, 0);

                Console.WriteLine("Running time: {0:hh\\:mm\\:ss}\t\t\n", runningTime);

                foreach (var engine in _monitoredEngines)
                {
                    Console.WriteLine("Monitoring engine {0} on address {1}, port {2} - last multicast data: {3}", engine.Name,
                        engine.multicastAddress, engine.multicastGroupPort, engine.lastMonitoredPacketTime);
                }

                if(_lastEngineLoadedOntoBackup!=null)
                {
                    Console.WriteLine("Last engine failed over to {0} backup engine : {1} at {2}", _backupEngine.Name, _lastEngineLoadedOntoBackup.Name, _lastEngineLoadedOntoBackup.lastFailoverTime);
                }

                CheckForStalledEngines();

                CheckForEngineRecovery();

                if (_currentFailedOverEngine != null)
                {
                    Console.WriteLine($@"Air engine {_currentFailedOverEngine.Name} is currently in fail over state (and will block any other fail over).");
                }

                Thread.Sleep(200);
            }

            foreach (var monitoringThread in _monitoringThreads)
            {
                if (monitoringThread.IsAlive)
                {
                    monitoringThread.Abort();
                }
            }
        }

        private static void CheckForStalledEngines()
        {
            foreach (var monitoredEngine in _monitoredEngines)
            {
                if (monitoredEngine.lastMonitoredPacketTime == new DateTime() != true)
                {
                    var timeSinceLastPacket = DateTime.Now.Subtract(monitoredEngine.lastMonitoredPacketTime);
                    if (timeSinceLastPacket > _airTimeout)
                    {
                        if (!monitoredEngine.isStalled)
                        {
                            monitoredEngine.isStalled = true;

                            LogMessage($@"Air engine {monitoredEngine.Name} has stalled for {timeSinceLastPacket}");
                        }
                        else
                        {
                            Console.WriteLine($@"Air engine {monitoredEngine.Name} has stalled for {timeSinceLastPacket}");
                        }

                        monitoredEngine.lastStallTime = DateTime.Now;

                        if (_currentFailedOverEngine == null)
                        {
                           TriggerEngineFailover(monitoredEngine);
                        }
                        else if(_currentFailedOverEngine != monitoredEngine)
                        {
                            Console.WriteLine("Engine {0} blocked from failing due to engine {1} already using backup engine.", monitoredEngine.Name, _currentFailedOverEngine.Name);
                        }
                    }
                    else
                    {
                        monitoredEngine.isStalled = false;
                    }
                }
            }
        }

        private static void CheckForEngineRecovery()
        {
            if (_currentFailedOverEngine != null)
            {
                var timeSinceLastStall = DateTime.Now.Subtract(_currentFailedOverEngine.lastStallTime);
                if (timeSinceLastStall > _airRecoveryTime)
                {
                    LogMessage($@"Air engine {_currentFailedOverEngine.Name} is considered recovered after passing the recovery timeout period");
                    _currentFailedOverEngine.isStalled = false;
                    _currentFailedOverEngine = null;
                }
            }
        }

        private static void TriggerEngineFailover(PlayoutEngine engine)
        {
            engine.lastFailoverTime = DateTime.Now;
            _currentFailedOverEngine = engine;
            _lastEngineLoadedOntoBackup = engine;

            LogMessage($@"Air engine {_currentFailedOverEngine.Name} is currently in fail over state (and will block any other fail over).");

            var fileToCopy = engine.Playlist;
            var sourceFilePathBackupVersion = engine.Playlist.Replace(".MCRActive",".bak");
            
            if(File.Exists(sourceFilePathBackupVersion))
            {
                fileToCopy = sourceFilePathBackupVersion;
            }

            if(!File.Exists(fileToCopy))
            {
                LogMessage("Cannot locate playlist {0} from engine {1}", fileToCopy, engine.Name);
            }

            LogMessage("Copying playlist {0} from engine {1} to backup engine {2}", fileToCopy, engine.Name, _backupEngine.Name);
        
            File.Copy(fileToCopy, _backupEngine.Playlist + ".Approved",true);

            if (Options.ShutdownOnFailover)
            {
                LogMessage("Shutdown on fail-over mode is enabled - watchdog now quitting after failover.");

                _pendingExit = true;
            }
        }

        private static void LoadConfig()
        {
            var doc = XElement.Load("settings.xml");

            var globalSettings = doc.Descendants("configuration").Descendants("globalSettings").FirstOrDefault();

            Options.AdapterAddress = globalSettings.Attribute("multicastListenAdapter").Value;
            var timeoutSeconds = int.Parse(globalSettings.Attribute("airEngineMulticastTimeout").Value);
            _airTimeout = new TimeSpan(0,0, timeoutSeconds);
            var recoverySeconds = int.Parse(globalSettings.Attribute("airEngineRecoveryStablePeriod").Value);
            _airRecoveryTime = new TimeSpan(0, 0, recoverySeconds);
            Options.ShutdownOnFailover = bool.Parse(globalSettings.Attribute("shutdownOnFailover").Value);

            var logfileName = doc.Descendants("configuration").Descendants("logFile").Descendants("FileName").FirstOrDefault();

            Options.LogFile = logfileName.Value;

            var engines = doc.Descendants("configuration").Descendants("playoutEngines").Descendants("engine");

            foreach (var engine in engines)
            {
                var playoutEngine = new PlayoutEngine
                {
                    Name = engine.Attribute("name").Value,
                    Playlist = engine.Attribute("playlist").Value,
                    MulticastUrl = engine.Attribute("multicastUrl").Value
                };
                _monitoredEngines.Add(playoutEngine);
            }

            var backupengineSettings = doc.Descendants("configuration").Descendants("backupEngine").Descendants("engine").FirstOrDefault();
            
            _backupEngine = new PlayoutEngine
            {
                Name = backupengineSettings.Attribute("name").Value,
                Playlist = backupengineSettings.Attribute("playlist").Value,
                MulticastUrl = backupengineSettings.Attribute("multicastUrl").Value
            };
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (_pendingExit) return; //already trying to exit - allow normal behaviour on subsequent presses
            _pendingExit = true;
            e.Cancel = true;
        }
        
        private static void LogMessage(string message, params object[] arguments)
        {
            var msg = Format(message, arguments);

            if (!Options.SuppressOutput)
            {
                Console.WriteLine(msg);
            }
            
            try
            {
                if (IsNullOrWhiteSpace(Options.LogFile)) return;

                var fs = new FileStream(Options.LogFile, FileMode.Append, FileAccess.Write);
                var sw = new StreamWriter(fs);

                sw.WriteLine("{0} - {1}", DateTime.Now, msg);

                sw.Close();
                fs.Close();
                sw.Dispose();
                fs.Dispose();
            }
            catch (Exception)
            {
                Debug.WriteLine("Concurrency error writing to log file...");
            }

        }

        private static void StartHttpService(string serviceAddress)
        {
            var baseAddress = new Uri(serviceAddress);

            _serviceHost?.Close();

            _airFailoverWatchdogApi = new AirFailoverWatchdogApi
            {

            };

            _airFailoverWatchdogApi.Command += AirFailoverWatchdogApiCommand;

            _serviceHost = new ServiceHost(_airFailoverWatchdogApi, baseAddress);
            var webBinding = new WebHttpBinding();

            var serviceEndpoint = new ServiceEndpoint(ContractDescription.GetContract(typeof(IAirFailoverWatchdogApi)))
            {
                Binding = webBinding,
                Address = new EndpointAddress(baseAddress)
            };

            _serviceHost.AddServiceEndpoint(serviceEndpoint);

            var webBehavior = new WebHttpBehavior
            {
                AutomaticFormatSelectionEnabled = true,
                DefaultOutgoingRequestFormat = WebMessageFormat.Json,
                HelpEnabled = true
            };

            serviceEndpoint.Behaviors.Add(webBehavior);

            //Metadata Exchange
            var serviceBehavior = new ServiceMetadataBehavior { HttpGetEnabled = true };
            _serviceHost.Description.Behaviors.Add(serviceBehavior);

            try
            {
                _serviceHost.Open();
            }
            catch (Exception ex)
            {
                var msg =
                    "Failed to start local web API - either something is already using the requested URL, the tool is not running as local administrator, or netsh url reservations have not been made " +
                    "to allow non-admin users to host services.\n\n" +
                    "To make a URL reservation, permitting non-admin execution, run:\n" +
                    "netsh http add urlacl http://+:8124/failover user=BUILTIN\\Users\n\n" +
                    "This is the details of the exception thrown:" +
                    ex.Message +
                    "\n\nHit enter to continue without services.\n\n";

                LogMessage(msg);

                Console.ReadLine();
            }
        }
        
        private static void AirFailoverWatchdogApiCommand(object sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case (CommandType.ResetMetrics):
                   //metrics don't exist in this app... just left this boilerplate code for later to copy from

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}

