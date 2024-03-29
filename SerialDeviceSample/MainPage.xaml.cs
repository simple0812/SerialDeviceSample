using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace SerialDeviceSample
{
    /// <summary>
    /// Implementation of the Modbus RTU via Raspberry pi UART0
    /// This example uses Windows.Devices.SerialCommunication.SerialDevice
    /// The user interface can be generated 03H Read Holding Registers command,
    /// But can also be enter other command in the "Command Buffer".
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MainViewModel mvm = null;

        public MainPage()
        {
            this.InitializeComponent();
            mvm = new MainViewModel();
            mainPanel.DataContext = mvm;
            resultsView.ItemsSource = mvm.Results;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as Button;
                btn.IsEnabled = false;
                await mvm.Handle(btn.Content.ToString());
                btn.IsEnabled = true;
                csbtn.IsEnabled = true;
            }
            catch (Exception ex)
            {

            }
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        byte address;
        byte function;
        ushort register;
        ushort length;
        int frequency;
        double writingTimeout;
        double readingTimeout;
        ObservableCollection<string> results;

        readonly uint[] _lookup32 = null;

        ModbusServer srv = null;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainViewModel()
        {
            if (srv == null)
            {
                // init serial device
                srv = new ModbusServer("UART0");
            }

            _lookup32 = createLookup32();
            address = 100;
            function = 3;
            register = 1;
            length = 1;
            frequency = 100;
            writingTimeout = 100;
            readingTimeout = 100;
            results = new ObservableCollection<string>();
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Address
        {
            get
            {
                return address.ToString("X2");
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    address = byte.Parse(value, NumberStyles.HexNumber);
                    function = 3;
                    this.OnPropertyChanged();
                    this.OnPropertyChanged("Modbus");
                    this.OnPropertyChanged("CRC");
                }
            }
        }

        public string Function
        {
            get
            {
                return function.ToString("X2");
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    function = byte.Parse(value, NumberStyles.HexNumber);
                    this.OnPropertyChanged();
                    this.OnPropertyChanged("Modbus");
                    this.OnPropertyChanged("CRC");
                }
            }
        }

        public string RegisterUpper
        {
            get
            {
                return (register >> 8).ToString("X2");
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    register = (ushort)((register & 0xFF) + (ushort.Parse(value, NumberStyles.HexNumber) << 8));
                    function = 3;
                    this.OnPropertyChanged();
                    this.OnPropertyChanged("Modbus");
                    this.OnPropertyChanged("CRC");
                }
            }
        }

        public string RegisterLower
        {
            get
            {
                return (register & 0xFF).ToString("X2");
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    register = (ushort)((register - (register & 0xFF)) + (ushort.Parse(value, NumberStyles.HexNumber) & 0xFF));
                    function = 3;
                    this.OnPropertyChanged();
                    this.OnPropertyChanged("Modbus");
                    this.OnPropertyChanged("CRC");
                }
            }
        }

        public string LengthUpper
        {
            get
            {
                return (length >> 8).ToString("X2");
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    length = (ushort)((length & 0xFF) + (ushort.Parse(value, NumberStyles.HexNumber) << 8));
                    function = 3;
                    this.OnPropertyChanged();
                    this.OnPropertyChanged("Modbus");
                    this.OnPropertyChanged("CRC");
                }
            }
        }

        public string LengthLower
        {
            get
            {
                return (length & 0xFF).ToString("X2");
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    length = (ushort)(length - (length & 0xFF) + (ushort.Parse(value, NumberStyles.HexNumber) & 0xFF));
                    function = 3;
                    this.OnPropertyChanged();
                    this.OnPropertyChanged("Modbus");
                    this.OnPropertyChanged("CRC");
                }
            }
        }

        public string Modbus
        {
            get
            {
                byte[] cmd = new byte[] {
                    address,
                    function,
                    (byte)(register >> 8),
                    (byte)(register & 0xFF),
                    (byte)(length >> 8),
                    (byte)(length & 0xFF)
                };

                string hex = byteArrayToHexViaLookup32(cmd);
                return hex;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    byte[] cmd = convertHexToBytes(value);
                    address = cmd[0];
                    function = cmd[1];
                    register = (ushort)((ushort)(cmd[2] << 8) + cmd[3]);
                    length = (ushort)((ushort)(cmd[4] << 8) + cmd[5]);
                    this.OnPropertyChanged();
                    this.OnPropertyChanged("CRC");
                }
            }
        }

        public string CRC
        {
            get
            {
                byte[] cmd = new byte[] {
                    address,
                    function,
                    (byte)(register >> 8),
                    (byte)(register & 0xFF),
                    (byte)(length >> 8),
                    (byte)(length & 0xFF)
                };

                var crc16 = modbusCrc(cmd, cmd.Length);

                string hex = byteArrayToHexViaLookup32(crc16);
                return hex;
            }
        }

        public int Frequency
        {
            get
            {
                return frequency;
            }
            set
            {
                frequency = value;
                OnPropertyChanged();
            }
        }

        //設定傳送逾時
        public double WritingTimeout
        {
            get
            {
                return writingTimeout;
            }
            set
            {
                writingTimeout = value;
                srv.SetWritingTimeout(writingTimeout);
                OnPropertyChanged();
            }
        }

        //設定接收逾時
        public double ReadingTimeout
        {
            get
            {
                return readingTimeout;
            }
            set
            {
                readingTimeout = value;
                srv.SetReadingTimeout(readingTimeout);
                OnPropertyChanged();
            }
        }

        public byte[] ModbusBuffer
        {
            get
            {
                byte[] cmd = new byte[] {
                    address,
                    function,
                    (byte)(register >> 8),
                    (byte)(register & 0xFF),
                    (byte)(length >> 8),
                    (byte)(length & 0xFF),
                    0,
                    0
                };

                var crc16 = modbusCrc(cmd, cmd.Length - 2);
                cmd[6] = (byte)crc16;
                cmd[7] = (byte)(crc16 >> 8);
                return cmd;
            }
        }

        public ObservableCollection<string> Results
        {
            get { return results; }
            set
            {
                if (results != value)
                {
                    results = value;
                    OnPropertyChanged();
                }

            }
        }

        private uint[] createLookup32()
        {
            var result = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                string s = i.ToString("X2");
                result[i] = s[0] + ((uint)s[1] << 16);
            }
            return result;
        }

        private string byteArrayToHexViaLookup32(byte[] bytes)
        {
            var lookup32 = _lookup32;
            var result = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                var val = lookup32[bytes[i]];
                result[2 * i] = (char)val;
                result[2 * i + 1] = (char)(val >> 16);
            }
            return new string(result);
        }

        private string byteArrayToHexViaLookup32(ushort word)
        {
            byte[] bytes = { (byte)word, (byte)(word >> 8) };
            var result = byteArrayToHexViaLookup32(bytes);
            return result;
        }

        private byte[] convertHexToBytes(string input)
        {
            var result = new byte[(input.Length + 1) / 2];
            var offset = 0;
            if (input.Length % 2 == 1)
            {
                result[0] = (byte)Convert.ToUInt32(input[0] + "", 16);
                offset = 1;
            }
            for (int i = 0; i < input.Length / 2; i++)
            {
                result[i + offset] = (byte)Convert.ToUInt32(input.Substring(i * 2 + offset, 2), 16);
            }
            return result;
        }

        ushort[] crcTable =
            {
            0X0000, 0XC0C1, 0XC181, 0X0140, 0XC301, 0X03C0, 0X0280, 0XC241,
            0XC601, 0X06C0, 0X0780, 0XC741, 0X0500, 0XC5C1, 0XC481, 0X0440,
            0XCC01, 0X0CC0, 0X0D80, 0XCD41, 0X0F00, 0XCFC1, 0XCE81, 0X0E40,
            0X0A00, 0XCAC1, 0XCB81, 0X0B40, 0XC901, 0X09C0, 0X0880, 0XC841,
            0XD801, 0X18C0, 0X1980, 0XD941, 0X1B00, 0XDBC1, 0XDA81, 0X1A40,
            0X1E00, 0XDEC1, 0XDF81, 0X1F40, 0XDD01, 0X1DC0, 0X1C80, 0XDC41,
            0X1400, 0XD4C1, 0XD581, 0X1540, 0XD701, 0X17C0, 0X1680, 0XD641,
            0XD201, 0X12C0, 0X1380, 0XD341, 0X1100, 0XD1C1, 0XD081, 0X1040,
            0XF001, 0X30C0, 0X3180, 0XF141, 0X3300, 0XF3C1, 0XF281, 0X3240,
            0X3600, 0XF6C1, 0XF781, 0X3740, 0XF501, 0X35C0, 0X3480, 0XF441,
            0X3C00, 0XFCC1, 0XFD81, 0X3D40, 0XFF01, 0X3FC0, 0X3E80, 0XFE41,
            0XFA01, 0X3AC0, 0X3B80, 0XFB41, 0X3900, 0XF9C1, 0XF881, 0X3840,
            0X2800, 0XE8C1, 0XE981, 0X2940, 0XEB01, 0X2BC0, 0X2A80, 0XEA41,
            0XEE01, 0X2EC0, 0X2F80, 0XEF41, 0X2D00, 0XEDC1, 0XEC81, 0X2C40,
            0XE401, 0X24C0, 0X2580, 0XE541, 0X2700, 0XE7C1, 0XE681, 0X2640,
            0X2200, 0XE2C1, 0XE381, 0X2340, 0XE101, 0X21C0, 0X2080, 0XE041,
            0XA001, 0X60C0, 0X6180, 0XA141, 0X6300, 0XA3C1, 0XA281, 0X6240,
            0X6600, 0XA6C1, 0XA781, 0X6740, 0XA501, 0X65C0, 0X6480, 0XA441,
            0X6C00, 0XACC1, 0XAD81, 0X6D40, 0XAF01, 0X6FC0, 0X6E80, 0XAE41,
            0XAA01, 0X6AC0, 0X6B80, 0XAB41, 0X6900, 0XA9C1, 0XA881, 0X6840,
            0X7800, 0XB8C1, 0XB981, 0X7940, 0XBB01, 0X7BC0, 0X7A80, 0XBA41,
            0XBE01, 0X7EC0, 0X7F80, 0XBF41, 0X7D00, 0XBDC1, 0XBC81, 0X7C40,
            0XB401, 0X74C0, 0X7580, 0XB541, 0X7700, 0XB7C1, 0XB681, 0X7640,
            0X7200, 0XB2C1, 0XB381, 0X7340, 0XB101, 0X71C0, 0X7080, 0XB041,
            0X5000, 0X90C1, 0X9181, 0X5140, 0X9301, 0X53C0, 0X5280, 0X9241,
            0X9601, 0X56C0, 0X5780, 0X9741, 0X5500, 0X95C1, 0X9481, 0X5440,
            0X9C01, 0X5CC0, 0X5D80, 0X9D41, 0X5F00, 0X9FC1, 0X9E81, 0X5E40,
            0X5A00, 0X9AC1, 0X9B81, 0X5B40, 0X9901, 0X59C0, 0X5880, 0X9841,
            0X8801, 0X48C0, 0X4980, 0X8941, 0X4B00, 0X8BC1, 0X8A81, 0X4A40,
            0X4E00, 0X8EC1, 0X8F81, 0X4F40, 0X8D01, 0X4DC0, 0X4C80, 0X8C41,
            0X4400, 0X84C1, 0X8581, 0X4540, 0X8701, 0X47C0, 0X4680, 0X8641,
            0X8201, 0X42C0, 0X4380, 0X8341, 0X4100, 0X81C1, 0X8081, 0X4040
        };

        private ushort modbusCrc(byte[] buffer, int length)
        {
            ushort crc = 0xFFFF;

            for (int i = 0; i < length; i++)
            {
                crc = (ushort)((crc >> 8) ^ crcTable[(crc ^ buffer[i]) & 0xFF]);
            }
            return crc;
        }

        public async Task Handle(string type)
        {
            string msgTemp = "TxD:{0} || RxD: \"{1}\"";
            string msgTempDebug = "TxD:{0} Elapsed time:{1}ms\r\nRxD: \"{2}\" Elapsed time:{3}ms";

            string txdMsg = string.Format(" data=\"{0}\", crc=\"{1}\" ", Modbus, CRC);
            string rxdMsg = "";

            //modbus request
            string command = Modbus + CRC;
            byte[] cache = convertHexToBytes(command);

            var buffer = cache.AsBuffer();
            byte[] result;

            switch (type)
            {
                case "Sending":
                    try
                    {
                        await srv.Open(WritingTimeout, ReadingTimeout);
                        await srv.Send(buffer);
                        var received = await srv.Receive();
                        srv.Clear();

                        //modbus response
                        result = received.ToArray();

                        /*
                        //byte convert to string sample
                        Collection<string> resultStringCollection = new Collection<string>();
                        foreach (var b in result)
                        {
                            resultStringCollection.Add(b.ToString("X2"));
                        }
                        */
                        rxdMsg = byteArrayToHexViaLookup32(result);
                    }
                    catch (Exception ex)
                    {
                        rxdMsg = ex.Message.Replace("\r\n", "\\r\\n");
                    }

                    Results.Insert(0, string.Format(msgTemp, txdMsg, rxdMsg));
                    break;
                case "Continuous sending":
                    try
                    {
                        Results.Insert(0, "Start sending");
                        await srv.Open(WritingTimeout, ReadingTimeout);

                        for (int i = 0; i < Frequency; i++)
                        {
                            var begin = DateTime.Now;
                            await srv.Send(buffer);
                            var writeSpan = DateTime.Now;

                            var received = await srv.Receive();
                            var readSpan = DateTime.Now;

                            srv.Clear();

                            //modbus response
                            result = received.ToArray();
                            rxdMsg = byteArrayToHexViaLookup32(result);

                            Results.Insert(0, string.Format(msgTempDebug, txdMsg, (writeSpan - begin).TotalMilliseconds, rxdMsg, (readSpan - writeSpan).TotalMilliseconds));
                        }
                    }
                    catch (Exception ex)
                    {
                        rxdMsg = ex.Message.Replace("\r\n", "\\r\\n");
                        Results.Insert(0, string.Format(msgTemp, txdMsg, rxdMsg));
                    }
                    Results.Insert(0, "End sending");
                    break;
                case "Reconnect":
                    if (srv.Status == ModbusServer.ModbusServerStatus.Opened)
                    {
                        srv.Cancel();
                        srv.Close();
                        srv = null;
                        srv = new ModbusServer("UART0");
                        await srv.Open();
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
