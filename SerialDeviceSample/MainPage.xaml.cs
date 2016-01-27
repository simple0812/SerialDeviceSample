using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
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
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            string msgTemp = "TxD:{0} || RxD: \"{1}\"";
            string txdStr = string.Format(" data=\"{0}\", crc=\"{1}\" ", txtModbus.Text, txtCRC.Text);
            string rxdStr = "";

            try
            {
                // init serial device
                string aqs = SerialDevice.GetDeviceSelector("UART0");
                var dis = await DeviceInformation.FindAllAsync(aqs);
                SerialDevice serialPort = await SerialDevice.FromIdAsync(dis[0].Id);

                // request timeout
                serialPort.WriteTimeout = new TimeSpan(0, 0, 1);
                // response timeout
                serialPort.ReadTimeout = new TimeSpan(0, 0, 3);

                using (var writer = new DataWriter(serialPort.OutputStream))
                {
                    // convert modbus command hex string to byte[]
                    var modbus = MainViewModel.ConvertHexToBytes(txtModbus.Text + txtCRC.Text);

                    // set modbus command in writer
                    writer.WriteBuffer(modbus.AsBuffer());

                    using (var cts = new CancellationTokenSource(serialPort.WriteTimeout))
                    {
                        // send request
                        await writer.StoreAsync().AsTask(cts.Token);
                    }

                    writer.DetachStream();
                }

                using (var reader = new DataReader(serialPort.InputStream))
                {
                    using (var cts = new CancellationTokenSource(serialPort.ReadTimeout))
                    {
                        // set buffer length
                        uint numBytesLoaded = await reader.LoadAsync((uint)2048).AsTask(cts.Token);

                        // get response
                        string text = reader.ReadString(numBytesLoaded);

                        reader.DetachStream();
                    }
                }
            }
            catch (Exception ex)
            {
                rxdStr = ex.Message.Replace("\r\n", "\\r\\n");
            }

            txtResult.Items.Insert(0, string.Format(msgTemp, txdStr, rxdStr));
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        byte address;
        byte function;
        ushort register;
        ushort length;
        static readonly uint[] _lookup32 = CreateLookup32();

        public event PropertyChangedEventHandler PropertyChanged;

        public MainViewModel()
        {
            address = 1;
            function = 3;
            register = 1;
            length = 1;
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

                string hex = ByteArrayToHexViaLookup32(cmd);
                return hex;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    byte[] cmd = ConvertHexToBytes(value);
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

                var crc16 = ModbusCrc(cmd, cmd.Length);

                string hex = ByteArrayToHexViaLookup32(crc16);
                return hex;
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

                var crc16 = ModbusCrc(cmd, cmd.Length - 2);
                cmd[6] = (byte)crc16;
                cmd[7] = (byte)(crc16 >> 8);
                return cmd;
            }
        }

        private static uint[] CreateLookup32()
        {
            var result = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                string s = i.ToString("X2");
                result[i] = s[0] + ((uint)s[1] << 16);
            }
            return result;
        }

        public static string ByteArrayToHexViaLookup32(byte[] bytes)
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

        public static string ByteArrayToHexViaLookup32(ushort word)
        {
            byte[] bytes = { (byte)word, (byte)(word >> 8) };
            var result = ByteArrayToHexViaLookup32(bytes);
            return result;
        }

        public static byte[] ConvertHexToBytes(string input)
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

        static ushort[] CrcTable =
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

        public static ushort ModbusCrc(byte[] buffer, int length)
        {
            ushort crc = 0xFFFF;

            for (int i = 0; i < length; i++)
            {
                crc = (ushort)((crc >> 8) ^ CrcTable[(crc ^ buffer[i]) & 0xFF]);
            }
            return crc;
        }
    }
}
