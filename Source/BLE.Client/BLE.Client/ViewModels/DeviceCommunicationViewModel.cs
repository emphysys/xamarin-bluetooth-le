using MvvmCross.Commands;
using MvvmCross.ViewModels;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;
using MvvmCross;
using MvvmCross.Navigation;
using System.Runtime.InteropServices;
using Xamarin.Forms.Internals;
using System.IO;
using Xamarin.Essentials;

namespace BLE.Client.ViewModels
{
    public class DeviceCommunicationViewModel : BaseViewModel
    {
        /// <summary>
        /// The prompt that the board returns after a command. 
        /// </summary>
        private const string CONSOLE_PROMPT = "emp_bm>";

        #region BINDINGS

        #region PROPERTIES

        private string _Command;
        /// <summary>
        /// The command to send to the board.
        /// </summary>
        public string Command
        {
            get => _Command;
            set
            {
                _Command = value;
                RaisePropertyChanged();
            }
        }

        private string _TraceVariable;
        /// <summary>
        /// The variable to trace.
        /// </summary>
        public string TraceVariable
        {
            get => _TraceVariable;
            set
            {
                _TraceVariable = value;
                RaisePropertyChanged();
            }
        }

        private string _Response;
        /// <summary>
        /// The response from the board.
        /// </summary>
        public string Response
        {
            get => _Response;
            set
            {
                _Response = value;

                RaisePropertyChanged();
            }
        }

        private string _PlotWindowRange;
        /// <summary>
        /// The range of the plot window, in seconds.
        /// </summary>
        public string PlotWindowRange
        {
            get => _PlotWindowRange;
            set
            {
                _PlotWindowRange = value;
                RaisePropertyChanged();

                if (double.TryParse(value, out var res)
                    && PlotModel != null && PacketAxis != null)
                {
                    // Calculate the delta (as a double) between two times *res* s apart
                    var now = DateTime.Now;
                    var future = now.AddSeconds(res);
                    var delta = DateTimeAxis.ToDouble(future) - DateTimeAxis.ToDouble(now);
                    PacketAxis.MaximumRange = delta;
                }
            }
        }

        private static PlotModel _PlotModel;
        public PlotModel PlotModel
        {
            get => _PlotModel;
            set
            {
                _PlotModel = value;
                RaisePropertyChanged(nameof(PlotModel));
            }
        }

        private double _scaleX, _scaleY;
        public double PlotViewScaleX
        {
            get => _scaleX;
            set
            {
                _scaleX = value;
                RaisePropertyChanged();
            }
        }
        public double PlotViewScaleY
        {
            get => _scaleY;
            set
            {
                _scaleY = value;
                RaisePropertyChanged();
            }
        }

        private bool _EnableSendToServer;
        public bool EnableSendToServer
        {
            get => _EnableSendToServer && PlotModel != null && PlotSeries != null && PlotSeries.Points.Count > 0;
            set
            {
                _EnableSendToServer = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region METHODS
        // These are referenced as button commands in the xaml
        //public MvxCommand StartTrace => new MvxCommand(Test);
        public MvxCommand StartTrace => new MvxCommand(StartTracing);
        public MvxCommand StopTrace => new MvxCommand(StopTracing);
        public MvxCommand SendCommand => new MvxCommand(() => SendCommandToBoard(Command));
        public MvxCommand ClearPlot => new MvxCommand(ClearPlotData);
        public MvxCommand ClearText => new MvxCommand(ClearTextData);
        public MvxCommand SendDataToServer => new MvxCommand(SendDataToAzureServer);
         
        #endregion

        #endregion

        #region PACKET RESPONSE STUFF

        // Relevant for parsing data out of bluetooth packets
        private const byte PACKETID_PRINTF = 0xaa;
        private const byte PACKETID_SCAN = 0xbb;
        private const byte PACKETID_TRACE = 0xcc;
        private const byte PACKETID_PROXY = 0xdd;
        private const byte PACKETID_PACKING = 0xee;

        /// <summary>
        /// The state of packet parsing.
        /// </summary>
        private enum ResponseState { WaitForID, WaitForLen1, WaitForLen2, WaitForPacket }

        /// <summary>
        /// The current packet parsing state.
        /// </summary>
        private ResponseState state;
        #endregion

        /// <summary>
        /// The RX and TX characteristics through which to communicate.
        /// </summary>
        private ICharacteristic rx, tx;

        /// <summary>
        /// Any leftover bytes from previous packets. Null if there is no leftover data.
        /// </summary>
        private byte[] unfinishedTxPacketBytes = null;

        /// <summary>
        /// The series (collection of points) to use. If PlotModel is null, this will throw!
        /// </summary>
        private LineSeries PlotSeries { get { return PlotModel.Series[0] as LineSeries; } }

        /// <summary>
        /// The x-axis. If PlotModel is null, this will throw!
        /// </summary>
        private DateTimeAxis PacketAxis { get { return PlotModel.Axes[0] as DateTimeAxis; } }


        public DeviceCommunicationViewModel(IAdapter adapter) : base(adapter)
        {
            Command = "ls";
            PlotWindowRange = "5";
            TraceVariable = "/ecgspi/ecg_ll_norm";
            state = ResponseState.WaitForID;

            PlotModel = InitPlotModel();

            PlotModel.InvalidatePlot(true);
            RaisePropertyChanged(nameof(PlotModel));
        }


        #region PLOT STUFF

        /// <summary>
        /// Initializes the plot legend and axes.
        /// </summary>
        /// <returns></returns>
        private PlotModel InitPlotModel()
        {
            var model = new PlotModel()
            {
                Title = "Press 'Trace' to Plot Trace Variable",
                TitleColor = OxyColors.DarkRed
            };

            var packetAxis = new DateTimeAxis()
            {
                Position = AxisPosition.Bottom,
                Title = "Time",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                StringFormat = "HH:mm:ss",
                IntervalLength = 65
            };
            model.Axes.Add(packetAxis);

            var valueAxis = new LinearAxis()
            {
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Title = "Value"
            };
            model.Axes.Add(valueAxis);

            var serie = new LineSeries
            {
                StrokeThickness = 2,
                CanTrackerInterpolatePoints = false,
            };

            model.Series.Add(serie);

            return model;
        }

        /// <summary>
        /// Adds the given point to the plot.
        /// </summary>
        /// <param name="point"></param>
        private void AddPointToGraph(DataPoint point)
        {
            var points = PlotSeries.Points;

            lock (PlotModel.SyncRoot)
            {
                points.Add(point);
            }

            PlotModel.InvalidatePlot(true);
        }

        /// <summary>
        /// Formats the plot data as a CSV.
        /// </summary>
        /// <returns>A \n-delimited CSV.</returns>
        public static string GetPlotPointsAsCSVString()
        {
            var builder = new StringBuilder((int)CSVDataSizeInBytes);
            plotPoints
                .Select(dp => $"{DateTimeAxis.ToDateTime(dp.X):HH:mm:ss-fff}, {dp.Y}\n")
                .ForEach(s => builder.Append(s));
            return builder.ToString();
        }

        public static void GetPlotPointsAsSVG(Stream imageStream, string title)
        {
            ExportPlotToStream(imageStream, new SvgExporter(), title);  
        }

        public static void GetPlotPointsAsPDF(Stream pdfStream)
        {
            throw new NotImplementedException("PDFExporter seems to be broken.");
        }

        private static void ExportPlotToStream(Stream stream, IExporter exporter, string title)
        {
            var points = (_PlotModel.Series[0] as LineSeries).Points;

            points.Clear();
            points.AddRange(plotPoints);

            _PlotModel.Title = title;
            (_PlotModel.Series[0] as LineSeries).Color = OxyColors.Black;
            _PlotModel.TitleColor = OxyColors.Black;

            exporter.Export(_PlotModel, stream); 
        }

        public static IPlotModel GetPlotModel()
        {
            return _PlotModel;
        }

        #endregion

        #region COMMANDS AND TRACING

        /// <summary>
        /// The cancellation token for the async plot clean function. Cancelled when tracing ends. 
        /// </summary>
        private CancellationTokenSource cleanPlotTokenSource;

        /// <summary>
        /// Begins tracing the variable specified in local property *TraceVariable*.
        /// </summary>
        public async void StartTracing()
        {
            EnableSendToServer = false;

            var traceCommand = $"trace {TraceVariable}";
            string[] cmds =
            {
                traceCommand,
                "trace_start"
            };

            PlotModel.Title = traceCommand;
            PlotModel.TitleColor = OxyColors.ForestGreen;
            PlotSeries.Color = OxyColors.ForestGreen;

            var token = new CancellationToken();

            foreach (var cmd in cmds)
            {
                Console.WriteLine($"Writing {cmd}");

                var packets = GetCommandPackets(cmd);
                foreach (var packet in packets)
                {
                    await rx.WriteAsync(packet, token);
                }
            }

            cleanPlotTokenSource = new CancellationTokenSource();

            var thread = new Thread(CleanPlotThreadEntry);
            thread.Start(cleanPlotTokenSource.Token);

            // Record the current time as the 0th trace packet time 
            traceTime = DateTime.Now;
        }

        /// <summary>
        /// A collection of old data points that have been cleaned from the plot to prevent the library from seizing over having too much data. 
        /// After tracing ends, this will hold all of the data points from the trace.
        /// </summary>
        private static List<DataPoint> plotPoints;

        /// <summary>
        /// The number of data points that the plot data structure can hold before old data should be cleaned out. 
        /// </summary>
        private const int CLEAN_PLOT_CAPACITY = 4096;

        /// <summary>
        /// The number of old data points to retain in the plot after a clean. 
        /// </summary>
        private const int CLEAN_PLOT_KEEP = 1024;

        /// <summary>
        /// The number of old data points to clean from the plot data structure.
        /// </summary>
        private const int CLEAN_PLOT_COPY = CLEAN_PLOT_CAPACITY - CLEAN_PLOT_KEEP;

        /// <summary>
        /// The number of ms to wait after a plot clean check.
        /// </summary>
        private const int CLEAN_PLOT_SLEEP_DURATION_MS = 1000;

        /// <summary>
        /// Asyncronous. Periodically cleans the plot data structure to prevent the library from freezing the app due to trying to plot too much data. 
        /// </summary>
        /// <param name="cancellationTokenAsObject">The cancellation token to consider when quitting this function</param>
        public void CleanPlotThreadEntry(object cancellationTokenAsObject)
        {
            var token = (CancellationToken)cancellationTokenAsObject;
            plotPoints = new List<DataPoint>();

            while (!token.IsCancellationRequested)
            {
                if (PlotSeries.Points.Count > CLEAN_PLOT_CAPACITY)
                { 
                    // Must lock the entire calculation because the main thread may try to add points during the copy calculations
                    lock (PlotModel.SyncRoot)
                    {
                        // Copy the first set of points to persistent storage
                        var overflow = PlotSeries.Points.Count - CLEAN_PLOT_CAPACITY;
                        var pointsToTake = PlotSeries.Points.Take(CLEAN_PLOT_COPY + overflow);
                        plotPoints.AddRange(pointsToTake);

                        var newPlotPoints = PlotSeries.Points.Skip(CLEAN_PLOT_COPY + overflow).ToList();
                        PlotSeries.Points.Clear();
                        PlotSeries.Points.AddRange(newPlotPoints);
                    }

                    Console.WriteLine("Cleaned plot");
                }

                Thread.Sleep(CLEAN_PLOT_SLEEP_DURATION_MS);
            }
        }

        /// <summary>
        /// Cancels and clears all tracing instructions.
        /// </summary>
        public async void StopTracing()
        {
            EnableSendToServer = true;

            cleanPlotTokenSource.Cancel();

            string[] cmds =
            {
                "trace_stop",
                "trace_clear"
            };

            PlotModel.Title = "Press 'Trace' to Plot Trace Variable";
            PlotModel.TitleColor = OxyColors.DarkRed;
            PlotSeries.Color = OxyColors.DarkRed;

            var token = new CancellationToken();
            foreach (var cmd in cmds)
            {
                var packets = GetCommandPackets(cmd);
                foreach (var packet in packets)
                {
                    await rx.WriteAsync(packet, token);
                }
            }

            // Copy any remaining points in the plot
            plotPoints.AddRange(PlotSeries.Points); 
        }

        /// <summary>
        /// Sends the given command to the board as an array of ASCII bytes.
        /// </summary>
        /// <param name="command">The command to send, without a line break.</param>
        public async Task SendCommandToBoard(string command)
        {
            Console.WriteLine("<== Command: {0}", command);

            // Write the command  
            var packets = GetCommandPackets(command);
            foreach (var packet in packets)
            {
                await rx.WriteAsync(packet); 
            }

            Console.WriteLine("<== Successfully wrote {0}", command);
        }

        /// <summary>
        /// Stops tracing and clears all data from the plot.
        /// </summary>
        private void ClearPlotData()
        {
            StopTracing();
            PlotSeries.Points.Clear();
            PlotSeries.PlotModel.InvalidatePlot(true);
            plotPoints.Clear();
            RaisePropertyChanged(nameof(EnableSendToServer));
        }

        private void ClearTextData()
        {
            Response = string.Empty;
        }

        /// <summary>
        /// Breaks a string command into a sequence of 20-byte packets to send to the board.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>Packets to send.</returns>
        private IEnumerable<byte[]> GetCommandPackets(string command)
        {
            // Append a newline if necessary
            var commandWithNewline = (command.Last() == '\n') ? command : $"{command}\n";

            var commandBytes = Encoding.ASCII.GetBytes(commandWithNewline);
            var packets = new List<byte[]>(commandBytes.Length / 20);

            int index = 0;
            while (index < commandBytes.Length)
            {
                var packet = commandBytes.Skip(index).Take(20).ToArray();
                packets.Add(packet);
                index += 20;
            }

            return packets;
        }

        #endregion

        #region AZURE
         
        public static long CSVDataSizeInBytes { get; private set; }

        /// <summary>
        /// Opens the data delivery screen.
        /// </summary>
        private async void SendDataToAzureServer()
        {
            CSVDataSizeInBytes = Marshal.SizeOf(typeof(DataPoint)) * plotPoints.Count; 
            await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<SendDataToServerViewModel, MvxBundle>(new MvxBundle(new Dictionary<string, string>()));
        }

        #endregion

        private DateTime traceTime;  

        private const float CONTROL_LOOP_SECONDS = 1 / 2000f;
        private const float CONTROL_LOOP_DIV = 10;

        /// <summary>
        /// Parses the updated TX value according to the structure of the packets that return from the board.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TxValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            // Prepend an unfinished message from a previous tx transaction if one exists
            byte[] buffer;
            if (unfinishedTxPacketBytes != null)
            {
                buffer = new byte[unfinishedTxPacketBytes.Length + tx.Value.Length];
                Array.Copy(unfinishedTxPacketBytes, buffer, unfinishedTxPacketBytes.Length);
                Array.Copy(tx.Value, 0, buffer, unfinishedTxPacketBytes.Length, tx.Value.Length);
                state = ResponseState.WaitForID;
            }
            else
            {
                buffer = tx.Value;
            }

            byte packetID = 0;
            int packetLength = 0, packetIndex = 0;
            byte[] packetData = new byte[0];
            int finishedPacketLastByteIndex = -1;

            // Parse data bytewise to interpret what's going on (code adapted from EmpExplorer)
            for (int i = 0; i < buffer.Length; i++)
            {
                var data = buffer[i];
                switch (state)
                {
                    case ResponseState.WaitForID:
                        if (data == PACKETID_PRINTF || data == PACKETID_SCAN || data == PACKETID_TRACE 
                            || data == PACKETID_PROXY || data == PACKETID_PACKING)
                        {
                            packetID = data;
                            state = ResponseState.WaitForLen1;
                        }
                        else
                        {
                            // Data returned unpackaged: skip directly to the WaitForPacket state and interpret data as ASCII. 
                            --i;
                            packetLength = buffer.Length;
                            packetID = PACKETID_PRINTF;
                            state = ResponseState.WaitForPacket;
                            packetData = new byte[packetLength];
                        }

                        break;
                    case ResponseState.WaitForLen1:
                        packetLength = data << 8;
                        state = ResponseState.WaitForLen2;
                        break;
                    case ResponseState.WaitForLen2:
                        packetLength |= data;
                        state = ResponseState.WaitForPacket;

                        packetData = new byte[packetLength];
                        packetIndex = 0;

                        if (packetLength <= 0)
                        {
                            state = ResponseState.WaitForID;
                        }

                        break;
                    case ResponseState.WaitForPacket:
                        packetData[packetIndex++] = data;
                        if (packetIndex == packetLength)
                        {
                            packetIndex = 0;
                            state = ResponseState.WaitForID;
                            finishedPacketLastByteIndex = i;

                            switch (packetID)
                            {
                                case PACKETID_PRINTF:
                                    var str = Encoding.ASCII.GetString(packetData).Replace(CONSOLE_PROMPT, "");
                                    Response += str; Console.Write(str);
                                    break; 
                                case PACKETID_TRACE: 
                                    var dataPointLength = 4;
                                    var numDataPointsInPacket = (double)packetLength / dataPointLength;
                                    if ((numDataPointsInPacket % 1) != 0)
                                    {
                                        throw new ArgumentException($"Packet length of {packetLength} is not divisible by data point size {dataPointLength}!");
                                    } 

                                    for (int j = 0; j < numDataPointsInPacket; j++)
                                    { 
                                        //var dataPoint = BitConverter.ToInt32(packetData, j * dataPointLength);
                                        var dataPoint = BitConverter.ToSingle(packetData, j * dataPointLength); 
                                        traceTime += TimeSpan.FromSeconds(CONTROL_LOOP_SECONDS * CONTROL_LOOP_DIV); 

                                        AddPointToGraph(new DataPoint(DateTimeAxis.ToDouble(traceTime), dataPoint));
                                    }

                                    break;
                                case PACKETID_PACKING:
                                    var dfasd = Thread.CurrentThread.ManagedThreadId;
                                    MainThread.BeginInvokeOnMainThread(() => SendCommandToBoard("set /trace/wait_for_packing 0"));
                                    break;
                                default:
                                    throw new FormatException($"Packet ID {packetID} is invalid or its data conversion is unimplemented in TxValueUpdated()!");
                            }
                        }
                        break;
                    default:
                        break;
                }
            }

            // If the current message spans multiple packets, store the remainder of the message for the next transmission
            if (finishedPacketLastByteIndex != buffer.Length - 1)
            {
                if (unfinishedTxPacketBytes != null)
                {
                    var newBuffer = new byte[buffer.Length - finishedPacketLastByteIndex - 1];
                    Array.Copy(buffer, finishedPacketLastByteIndex + 1, newBuffer, 0, newBuffer.Length);
                    unfinishedTxPacketBytes = newBuffer;
                }
                else
                {
                    unfinishedTxPacketBytes = new byte[buffer.Length - finishedPacketLastByteIndex - 1];
                    Array.Copy(buffer, finishedPacketLastByteIndex + 1, unfinishedTxPacketBytes, 0, buffer.Length - finishedPacketLastByteIndex - 1);
                }
            }
            else
            {
                unfinishedTxPacketBytes = null;
            }
        }

        #region BASEVIEWMODEL PREPARATION

        public override void Prepare(MvxBundle parameters)
        {
            base.Prepare(parameters); Console.WriteLine("Prepare");

            // Deal with async stuff
            PrepareAsync(parameters);
        }

        private async void PrepareAsync(MvxBundle parameters)
        {
            Console.WriteLine("PrepareAsync");

            Console.WriteLine("Findandstore");
            await FindAndStoreCharacteristics(parameters);
            Console.WriteLine("BeginUpdates");
            await BeginRxTxUpdates();
            Console.WriteLine("InitConnection");
            await InitConnection();
        }

        private readonly Guid UUID_SERVICE = Guid.Parse("0000fe60-cc7a-482a-984a-7f2ed5b3e58f");
        private readonly Guid UUID_RX = Guid.Parse("0000fe61-8e22-4541-9d4c-21edae82ed19");
        private readonly Guid UUID_TX = Guid.Parse("0000fe62-8e22-4541-9d4c-21edae82ed19");

        /// <summary>
        /// Finds and stores references to the RX and TX characteristics attached to the current device.
        /// </summary>
        /// <param name="parameters"></param>
        private async Task FindAndStoreCharacteristics(MvxBundle parameters)
        {
            // Get the characteristics from the device
            var device = GetDeviceFromBundle(parameters);
            var services = await device.GetServicesAsync();

            // Find the service and characteristics by UUID
            IService service = services.First(s => s.Id.Equals(UUID_SERVICE));

            var characteristics = await service.GetCharacteristicsAsync();

            rx = characteristics.First(c => c.Id.Equals(UUID_RX));
            tx = characteristics.First(c => c.Id.Equals(UUID_TX));
        }

        /// <summary>
        /// Starts TX updates and attaches value update delegates to rx and tx.
        /// </summary>
        private async Task BeginRxTxUpdates()
        {
            tx.ValueUpdated += TxValueUpdated;
            rx.ValueUpdated += TxValueUpdated;
            await tx.StartUpdatesAsync();
        }

        private async Task InitConnection()
        {
            // Enable prntf packets and set packet size, then stop and clear tracing
            string[] cmds =
            {
                "set /sys/prntf_packet_enable 1",
                "set /trace/packet_len 4",
                "trace_stop",
                "trace_clear",
                "set /trace/div 10"
            };

            foreach (var cmd in cmds)
            {
                await SendCommandToBoard(cmd);
            }
        }

        #endregion
    }
}
