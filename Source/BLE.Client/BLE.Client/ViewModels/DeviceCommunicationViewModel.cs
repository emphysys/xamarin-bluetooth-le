using MvvmCross.Commands;
using MvvmCross.ViewModels;
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

        /// <summary>
        /// The maximum number of packets to read.
        /// </summary>
        private int numPacketsToRead;

        /// <summary>
        /// String wrapper around the maximum number of packets to read.
        /// </summary>
        public string NumPacketsToReadStr
        {
            get => numPacketsToRead.ToString();
            set
            {
                numPacketsToRead = int.Parse(value);
                RaisePropertyChanged();
            }
        }
        
        #endregion

        #region METHODS

        public MvxCommand TraceHeartbeat => new MvxCommand(SendTraceHeartbeatCommands);
        public MvxCommand SendCommand => new MvxCommand(() => SendCommandToBoard(Command));
        public MvxCommand ClearResponseLabel => new MvxCommand(ClearResponseText);

        #endregion

        #endregion

        #region PACKET RESPONSE STUFF

        private const byte PACKETID_PRINTF = 0xaa;
        private const byte PACKETID_SCAN = 0xbb;
        private const byte PACKETID_TRACE = 0xcc;
        private const byte PACKETID_PROXY = 0xdd;

        private enum ResponseState { WaitForID, WaitForLen1, WaitForLen2, WaitForPacket }

        private ResponseState state;
        #endregion

        /// <summary>
        /// The RX and TX characteristics through which to communicate.
        /// </summary>
        private ICharacteristic rx, tx;

        /// <summary>
        /// The number of packets read so far.
        /// </summary>
        private int numPacketsRead;

        public DeviceCommunicationViewModel(IAdapter adapter) : base(adapter)
        {
            Command = "ls";
            numPacketsRead = 0;
            NumPacketsToReadStr = "128";
            state = ResponseState.WaitForID;
        }

        #region BUTTON DELEGATES
         
        public async void SendTraceHeartbeatCommands()
        {
            // Clear the response data
            Response = string.Empty;

            var token = new CancellationToken();

            // These commands follow the EMPExplorer code
            string[] cmds =
            {
                "trace_stop",
                "trace_clear",
                "trace /control/heartbeat_cnt",
                "set /trace/packet_len 4",
                "trace_start"
            };

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

        public async void SendCommandToBoard(string command)
        {
            // Clear the response data
            Response = string.Empty;

            // Write the command  
            var packets = GetCommandPackets(command);
            foreach (var packet in packets)
            {
                await rx.WriteAsync(packet, new CancellationToken());
            }
        }

        private void ClearResponseText()
        {
            Response = string.Empty;
        }
         
        #endregion

        private IEnumerable<byte[]> GetCommandPackets(string command)
        {
            Response = "Tracing /control/heartbeat:\n";
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
         
        /// <summary>
        /// Parses the updated TX value according to the structure of the packets that return from the board.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TxValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        { 
            // This function is rather a mess :)
            var buffer = tx.Value;
            byte packetID = 0;
            int packetLength = 0, packetIndex = 0;
            byte[] packetData = new byte[0]; 

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

                        if (packetLength == 0)
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

                            switch (packetID)
                            {
                                case PACKETID_PRINTF:
                                    var str = Encoding.ASCII.GetString(packetData).Replace(CONSOLE_PROMPT, "");
                                    Response += str;
                                    break;
                                case PACKETID_SCAN:
                                    // TODO
                                    break;
                                case PACKETID_TRACE:
                                    string format = "Read: {0}\n";
                                    string toPrint;
                                    switch (packetLength)
                                    {
                                        case 1:
                                            toPrint = string.Format(format, packetData[0]);
                                            break;
                                        case 2:
                                            toPrint = string.Format(format, BitConverter.ToInt16(packetData, 0));
                                            break;
                                        case 4:
                                            toPrint = string.Format(format, BitConverter.ToInt32(packetData, 0));
                                            break;
                                        case 8:
                                            toPrint = string.Format(format, BitConverter.ToInt64(packetData, 0)); 
                                            break;
                                        default:
                                            throw new NotImplementedException($"Packet length {packetLength} not implemented in TxValueUpdated()!");
                                    } 

                                    Response += toPrint; 
                                    break;
                                case PACKETID_PROXY:
                                    // TODO
                                    break;
                                default:
                                    throw new FormatException($"Packet ID {packetID} is invalid or its data conversion is unimplemented in TxValueUpdated()!");
                            }

                            // Increase the packet count and check if it's time to stop
                            if (numPacketsRead++ == numPacketsToRead)
                            {
                                var task = tx.StopUpdatesAsync();
                                task.Wait(TimeSpan.FromMilliseconds(500));
                                Response += $"Read {numPacketsToRead} packets - stopping updates.";

                                // !TODO how to proceed here
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }
        
        #region BASEVIEWMODEL PREPARATION
         
        public override void Prepare(MvxBundle parameters)
        {
            base.Prepare(parameters);

            FindAndStoreCharacteristics(parameters);
        }

        /// <summary>
        /// Finds and stores references to the RX and TX characteristics attached to the current device.
        /// </summary>
        /// <param name="parameters"></param>
        private async void FindAndStoreCharacteristics(MvxBundle parameters)
        {
            // Get the characteristics from the device
            var device = GetDeviceFromBundle(parameters);
            var services = await device.GetServicesAsync(); 
            var service = services[0];
            var characteristics = await service.GetCharacteristicsAsync();
             
            // RX first, TX second
            rx = characteristics[0]; tx = characteristics[1];

            //  Listen for updates on rx and tx
            BeginRxTxUpdates();
        }

        /// <summary>
        /// Starts TX updates and attaches value update delegates to rx and tx.
        /// </summary>
        private async void BeginRxTxUpdates()
        {
            tx.ValueUpdated += TxValueUpdated;
            rx.ValueUpdated += TxValueUpdated;
            await tx.StartUpdatesAsync();
        }

        #endregion
    }
}
