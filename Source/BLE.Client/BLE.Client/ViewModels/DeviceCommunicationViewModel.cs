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

        private PlotModel _PlotModel;
        public PlotModel PlotModel
        {
            get => _PlotModel;
            set
            {
                _PlotModel = value;
                RaisePropertyChanged(nameof(PlotModel));
            }
        }

        #endregion

        #region METHODS
        // These are referenced as button commands in the xaml
        public MvxCommand StartTrace => new MvxCommand(StartTracing);
        public MvxCommand StopTrace => new MvxCommand(StopTracing);
        public MvxCommand SendCommand => new MvxCommand(() => SendCommandToBoard(Command));
        public MvxCommand ClearData => new MvxCommand(ClearPrntfAndPlotData);

        #endregion

        #endregion

        #region PACKET RESPONSE STUFF

        // Relevant for parsing data out of bluetooth packets
        private const byte PACKETID_PRINTF = 0xaa;
        private const byte PACKETID_SCAN = 0xbb;
        private const byte PACKETID_TRACE = 0xcc;
        private const byte PACKETID_PROXY = 0xdd;

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
            TraceVariable = "/control/heartbeat_cnt";
            state = ResponseState.WaitForID;

            PlotModel = InitPlotModel();

            PlotModel.InvalidatePlot(true);
            RaisePropertyChanged(nameof(PlotModel));
        }


        #region PLOT STUFF

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
            
            lock(PlotModel.SyncRoot)
            {
                points.Add(point);
            }

            PlotModel.InvalidatePlot(true);
        }

        #endregion

        #region COMMANDS AND TRACING

        /// <summary>
        /// Begins tracing the variable specified in local property *TraceVariable*.
        /// </summary>
        public async void StartTracing()
        { 
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
        }

        /// <summary>
        /// Cancels and clears all tracing instructions.
        /// </summary>
        public async void StopTracing()
        {
            var token = new CancellationToken();

            string[] cmds =
            {
                "trace_stop",
                "trace_clear"
            };

            PlotModel.Title = "Press 'Trace' to Plot Trace Variable";
            PlotModel.TitleColor = OxyColors.DarkRed;
            PlotSeries.Color = OxyColors.DarkRed;

            foreach (var cmd in cmds)
            {
                var packets = GetCommandPackets(cmd);
                foreach (var packet in packets)
                {
                    await rx.WriteAsync(packet, token);
                }
            }
        }

        /// <summary>
        /// Sends the given command to the board as an array of ASCII bytes.
        /// </summary>
        /// <param name="command">The command to send, without a line break.</param>
        public async void SendCommandToBoard(string command)
        { 
            // Write the command  
            var packets = GetCommandPackets(command);
            foreach (var packet in packets)
            {
                await rx.WriteAsync(packet, new CancellationToken());
            }
        }

        /// <summary>
        /// Clears the prntf data and wipes the plot.
        /// </summary>
        private void ClearPrntfAndPlotData()
        {
            Response = string.Empty;

            PlotSeries.Points.Clear();
            PlotSeries.PlotModel.InvalidatePlot(true);
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
                        if (data == PACKETID_PRINTF || data == PACKETID_SCAN || data == PACKETID_TRACE || data == PACKETID_PROXY)
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
                                case PACKETID_SCAN:
                                    // TODO
                                    break;
                                case PACKETID_TRACE:  
                                    DataPoint dp;
                                    var datetime = DateTimeAxis.ToDouble(DateTime.Now);
                                    switch (packetLength)
                                    {
                                        case 1:  
                                            dp = new DataPoint(datetime, packetData[0]);
                                            break;
                                        case 2: 
                                            dp = new DataPoint(datetime, BitConverter.ToInt16(packetData, 0));
                                            break;
                                        case 4:
                                            dp = new DataPoint(datetime, BitConverter.ToInt32(packetData, 0));
                                            break;
                                        case 8: 
                                            dp = new DataPoint(datetime, BitConverter.ToInt64(packetData, 0));
                                            break;
                                        default:
                                            throw new NotImplementedException($"Packet length {packetLength} not implemented in TxValueUpdated()!");
                                    }

                                    AddPointToGraph(dp); 
                                    break;
                                case PACKETID_PROXY:
                                    // TODO
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
            base.Prepare(parameters);

            // Deal with async stuff
            PrepareAsync(parameters);
        }

        private async void PrepareAsync(MvxBundle parameters)
        {
            await FindAndStoreCharacteristics(parameters);
            await BeginRxTxUpdates();
            await InitConnection();
        }

        /// <summary>
        /// Finds and stores references to the RX and TX characteristics attached to the current device.
        /// </summary>
        /// <param name="parameters"></param>
        private async Task FindAndStoreCharacteristics(MvxBundle parameters)
        {
            // Get the characteristics from the device
            var device = GetDeviceFromBundle(parameters);
            var services = await device.GetServicesAsync();

            // The following is platform-dependent (needs to change as well probably)
            IService service = null;

            if (services.Count == 3)
            {
                // Android
                service = services[2];
            }
            else if (services.Count == 1)
            {
                // iOS
                service = services[0];
            }

            var characteristics = await service.GetCharacteristicsAsync();

            // RX first, TX second
            rx = characteristics[0]; tx = characteristics[1];
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
            };

            foreach (var cmd in cmds)
            {
                await Task.Run(() => SendCommandToBoard(cmd));
                await Task.Delay(500);
            } 
        }

#endregion
    }
}
