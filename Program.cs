﻿/*   Copyright 2016 Cinegy Gmbh  */

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
        private static bool _receiving;
        private static bool _suppressConsoleOutput;
        private static readonly Options Options = new Options();
        private static DateTime _startTime = DateTime.UtcNow;
        private static string _logFile;
        private static bool _pendingExit;
        private static ServiceHost _serviceHost;
        private static AirFailoverWatchdogApi _airFailoverWatchdogApi;
        private static UdpClient _udpClient = new UdpClient { ExclusiveAddressUse = false };

        private static List<PlayoutEngine> _monitoredEngines = new List<PlayoutEngine>();
        private static PlayoutEngine _backupEngine = new PlayoutEngine(); 

        private static NetworkMetric _networkMetric = new NetworkMetric();
        private static RtpMetric _rtpMetric = new RtpMetric();

        static void Main(string[] args)
        {

            Console.CancelKeyPress += Console_CancelKeyPress;

            Console.WriteLine("Cinegy Simple RTP monitoring tool v1.0.0 ({0})\n",
                File.GetCreationTime(Assembly.GetExecutingAssembly().Location));

            try
            {
                Console.SetWindowSize(100, 40);
            }
            catch
            {
                Console.WriteLine("Failed to increase console size - probably screen resolution is low");
            }

            if (!Parser.Default.ParseArguments(args, Options))
            {
                Console.WriteLine(
                    "\nThis application must have a configuration file specified for operation.");

                Console.WriteLine("\nHit enter to quit");

                Console.ReadLine();
                Environment.Exit(500);
            }

            SetupApplication();
           // WorkLoop();
        }

        private static void SetupApplication()
        {
            Console.Clear();

            LoadConfig();

            Console.ReadLine();

            //if (!IsNullOrWhiteSpace(Options.LogFile))
            //{
            //    PrintToConsole("Logging events to file {0}", _logFile);
            //}
            //LogMessage("Logging started.");

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

        private static void WorkLoop()
        {
           
            while (!_pendingExit)
            {
                var runningTime = DateTime.UtcNow.Subtract(_startTime);

                if (runningTime.Milliseconds < 20)
                {
                   // Console.Clear();
                }

                Console.SetCursorPosition(0, 0);

                PrintToConsole("Running time: {0:hh\\:mm\\:ss}\t\t\n", runningTime);

                Thread.Sleep(20);
            }
        }

        //private static void WorkLoop(Options _options)
        //{
        //    Console.Clear();

        //    if (!_receiving)
        //    {
        //        _receiving = true;
        //        _logFile = _options.LogFile;
        //        _suppressConsoleOutput = _options.SuppressOutput;

        //        if (!IsNullOrWhiteSpace(_logFile))
        //        {
        //            PrintToConsole("Logging events to file {0}", _logFile);
        //        }
        //        LogMessage("Logging started.");

        //        if (_options.EnableWebServices)
        //        {
        //            var httpThreadStart = new ThreadStart(delegate
        //            {
        //                StartHttpService(_options.ServiceUrl);
        //            });

        //            var httpThread = new Thread(httpThreadStart) { Priority = ThreadPriority.Normal };

        //            httpThread.Start();
        //        }

        //        SetupMetrics();
        //        StartListeningToNetwork(_options.MulticastAddress, _options.MulticastGroup, _options.AdapterAddress);
        //    }

        //    Console.Clear();

        //    while (!_pendingExit)
        //    {
        //        var runningTime = DateTime.UtcNow.Subtract(_startTime);

        //        //causes occasional total refresh to erase glitches that build up
        //        if (runningTime.Milliseconds < 20)
        //        {
        //            Console.Clear();
        //        }

        //        if (!_suppressConsoleOutput)

        //        {
        //            Console.SetCursorPosition(0, 0);

        //            PrintToConsole("URL: rtp://@{0}:{1}\tRunning time: {2:hh\\:mm\\:ss}\t\t\n", _options.MulticastAddress,
        //                _options.MulticastGroup, runningTime);
        //            PrintToConsole(
        //                "Network Details\n----------------\nTotal Packets Rcvd: {0} \tBuffer Usage: {1:0.00}%\t\t\nTotal Data (MB): {2}\t\tPackets per sec:{3}",
        //                _networkMetric.TotalPackets, _networkMetric.NetworkBufferUsage, _networkMetric.TotalData / 1048576,
        //                _networkMetric.PacketsPerSecond);
        //            PrintToConsole("Time Between Packets (ms): {0} \tShortest/Longest: {1}/{2}",
        //                _networkMetric.TimeBetweenLastPacket, _networkMetric.ShortestTimeBetweenPackets,
        //                _networkMetric.LongestTimeBetweenPackets);
        //            PrintToConsole("Bitrates (Mbps): {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00} (Current/Avg/Peak/Low)\t\t\t",
        //                (_networkMetric.CurrentBitrate / 131072.0), _networkMetric.AverageBitrate / 131072.0,
        //                (_networkMetric.HighestBitrate / 131072.0), (_networkMetric.LowestBitrate / 131072.0));
        //            PrintToConsole(
        //                "\nRTP Details\n----------------\nSeq Num: {0}\tMin Lost Pkts: {1}\nTimestamp: {2}\tSSRC: {3}\t",
        //                _rtpMetric.LastSequenceNumber, _rtpMetric.MinLostPackets, _rtpMetric.LastTimestamp, _rtpMetric.Ssrc);

        //            if (null != _serviceDescriptionTable)
        //            {
        //                lock (_serviceDescriptionTableLock)
        //                {
        //                    if (_serviceDescriptionTable.Sections != null)
        //                    {
        //                        foreach (ServiceDescriptionTable.Section section in _serviceDescriptionTable.Sections)
        //                        {
        //                            PrintToConsole(
        //                                "Service Information\n----------------\nService Name {0}\tService Provider {1}\n\t\t\t\t\t\t\t\t\t\t",
        //                                section.ServiceName, section.ServiceProviderName);
        //                        }
        //                    }
        //                }
        //            }

        //            PrintToConsole("\nTS Details\n----------------");
        //            lock (_tsMetrics)
        //            {
        //                var patMetric = _tsMetrics.FirstOrDefault(m => m.IsProgAssociationTable);
        //                if (patMetric?.ProgAssociationTable.ProgramNumbers != null)
        //                {
        //                    PrintToConsole("Unique PID count: {0}\t\tProgram Count: {1}\t\t\nShowing up to 10 PID streams in table:", _tsMetrics.Count,
        //                        patMetric.ProgAssociationTable.ProgramNumbers.Length);
        //                }

        //                foreach (var tsMetric in _tsMetrics.OrderByDescending(m => m.Pid).Take(10))
        //                {
        //                    PrintToConsole("TS PID: {0}\tPacket Count: {1} \t\tCC Error Count: {2}\t", tsMetric.Pid,
        //                        tsMetric.PacketCount, tsMetric.CcErrorCount);
        //                }
        //            }

        //        }

        //        Thread.Sleep(20);
        //    }

        //    LogMessage("Logging stopped.");
        //}

        private static void LoadConfig()
        {
            var doc = XElement.Load("settings.xml");

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


        }

        private static void StartListeningToNetwork(string multicastAddress, int multicastGroup,
            string listenAdapter = "")
        {

            var listenAddress = IsNullOrEmpty(listenAdapter) ? IPAddress.Any : IPAddress.Parse(listenAdapter);

            var localEp = new IPEndPoint(listenAddress, multicastGroup);

            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.ReceiveBufferSize = 1024*256;
            _udpClient.ExclusiveAddressUse = false;
            _udpClient.Client.Bind(localEp);
            _networkMetric.UdpClient = _udpClient;

            var parsedMcastAddr = IPAddress.Parse(multicastAddress);
            _udpClient.JoinMulticastGroup(parsedMcastAddr);

            var ts = new ThreadStart(delegate
            {
                ReceivingNetworkWorkerThread(_udpClient, localEp);
            });

            var receiverThread = new Thread(ts) {Priority = ThreadPriority.Highest};

            receiverThread.Start();
        }

        private static void ReceivingNetworkWorkerThread(UdpClient client, IPEndPoint localEp)
        {
            while (_receiving)
            {
                var data = client.Receive(ref localEp);
                if (data == null) continue;
                try
                {
                    _networkMetric.AddPacket(data);
                    _rtpMetric.AddPacket(data);              
                }
                catch (Exception ex)
                {
                    LogMessage($@"Unhandled exception withing network receiver: {ex.Message}");
                }
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (_pendingExit) return; //already trying to exit - allow normal behaviour on subsequent presses
            _pendingExit = true;
            e.Cancel = true;
        }
        
        private static void RtpMetric_SequenceDiscontinuityDetected(object sender, EventArgs e)
        {
            LogMessage("Discontinuity in RTP sequence.");
        }

        private static void NetworkMetric_BufferOverflow(object sender, EventArgs e)
        {
            LogMessage("Network buffer > 99% - probably loss of data from overflow.");
        }

        private static void PrintToConsole(string message, params object[] arguments)
        {
            if (_suppressConsoleOutput) return;
            
            Console.WriteLine(message, arguments);
        }

        private static void LogMessage(string message)
        {
            try
            {
                if (IsNullOrWhiteSpace(_logFile)) return;

                var fs = new FileStream(_logFile, FileMode.Append, FileAccess.Write);
                var sw = new StreamWriter(fs);

                sw.WriteLine("{0} - {1}", DateTime.Now, message);

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
                NetworkMetric = _networkMetric,
                RtpMetric = _rtpMetric
            };

            _airFailoverWatchdogApi.Command += AirFailoverWatchdogApiCommand;
            
            _serviceHost = new ServiceHost(_airFailoverWatchdogApi, baseAddress);
            var webBinding = new WebHttpBinding();

            var serviceEndpoint = new ServiceEndpoint(ContractDescription.GetContract(typeof (IAirFailoverWatchdogApi)))
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
            var serviceBehavior = new ServiceMetadataBehavior {HttpGetEnabled = true};
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

                Console.WriteLine(msg);

                Console.ReadLine();

                LogMessage(msg);
            }
        }

        private static void SetupMetrics()
        {
            _startTime = DateTime.UtcNow;
            _networkMetric = new NetworkMetric();
            _rtpMetric = new RtpMetric();
            _rtpMetric.SequenceDiscontinuityDetected += RtpMetric_SequenceDiscontinuityDetected;
            _networkMetric.BufferOverflow += NetworkMetric_BufferOverflow;
            _networkMetric.UdpClient = _udpClient;
        }

        private static void AirFailoverWatchdogApiCommand(object sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case (CommandType.ResetMetrics):
                    SetupMetrics();

                    _airFailoverWatchdogApi.NetworkMetric = _networkMetric;
                    _airFailoverWatchdogApi.RtpMetric = _rtpMetric;

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}

