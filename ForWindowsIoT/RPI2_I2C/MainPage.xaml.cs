using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RPI2_I2C
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private const byte I2C_ADDR_PCF8591 = 0x48;  // Adres PCF8591; Adres ma 127 bitów, więc 0x90 >> 1 = 0x48
        private const byte I2C_ADDR_MCP23008 = 0x20; // Adres MCP23008
        private const byte I2C_ADDR_ARDUINO = 17;    // Adres Arduino (patrz też ForArduino\i2cCommunication.ino)

        private const byte MCP23008_IODIR = 0x00;    // Rejestr IODIR (kierunek komunikacji w GPIO)
        private const byte MCP23008_GPIO = 0x09;     // Rejestr GPIO uzywany do odczytu wartości pin (tu nieużywane)
        private const byte MCP23008_OLAT = 0x0A;     // Zatrzask (Latch register) wyjściowy używany do ustawiania pinu (0/1)

        private const byte LED_GPIO_PIN = 0x1;       // Numer PINu na MCP23008 gdzie dołączana dioda / laser
        I2cDevice i2cMCP23008 = null;

        private byte iodirRegister; // lokalna kopia IODIR z MCP230008
        private byte gpioRegister;  // lokalna kopia IODIR z MCP230008
        private byte olatRegister;  // lokalna kopia IODIR z MCP230008

        I2cDevice i2cPCF8591 = null;

        I2cDevice i2cArduino = null;

        DispatcherTimer m_timer;


        /* Podłączenia PCF8591:
        A0, A1, A2, Vss do GND
        Vdd, Vref do + 5V
        AGND - do GND
        EXT - zostawic puste!!!
        SCL, SDA - I2C

        Adres I2C: 0x90 >> 1
        */
        /* Podłączenia MCP23008:
        SCL, SDA - I2C
        A2, A1, A0 do GND
        RESET do +5V
        Vss do GND
        Vdd do +5V
        Adres I2C: 0x20
        */
		
        /* Jakby było używane PCF8574 to adres I2C: 0x27
        */


        public MainPage()
        {
            this.InitializeComponent();
            InitI2C();
        }
        private async void InitI2C() {
            byte[] i2CWriteBuffer;
            byte[] i2CReadBuffer;
            byte bitMask;

            // Inicjalizacja I2C - urządzenie z RPI2
            string deviceSelector = I2cDevice.GetDeviceSelector();
            var i2cDeviceControllers = await DeviceInformation.FindAllAsync(deviceSelector);
            if (i2cDeviceControllers.Count == 0) {
                return;
            }

            //Ustawienia dla MCP230008
            var i2cSettings = new I2cConnectionSettings(I2C_ADDR_MCP23008);
            i2cSettings.BusSpeed = I2cBusSpeed.FastMode;
            i2cMCP23008 = await I2cDevice.FromIdAsync(i2cDeviceControllers[0].Id, i2cSettings);
            if (i2cMCP23008 == null) {
                return;
            }

            //Ustawienia dla PCF8591
            i2cSettings.SlaveAddress = I2C_ADDR_PCF8591;
            i2cPCF8591 = await I2cDevice.FromIdAsync(i2cDeviceControllers[0].Id, i2cSettings);
            if (i2cPCF8591 == null) {
                return;
            }

            //Ustawienia dla Arduino
            i2cSettings.SlaveAddress = I2C_ADDR_ARDUINO;
            i2cArduino = await I2cDevice.FromIdAsync(i2cDeviceControllers[0].Id, i2cSettings);
            if (i2cArduino == null) {
                return;
            }


            // Inicjalizacja port Expander 
			// Za: https://ms-iot.github.io/content/en-US/win10/samples/I2CPortExpander.htm 
            try {
                i2CReadBuffer = new byte[1];
                i2cMCP23008.WriteRead(new byte[] { MCP23008_IODIR }, i2CReadBuffer);
                iodirRegister = i2CReadBuffer[0];
                i2cMCP23008.WriteRead(new byte[] { MCP23008_GPIO }, i2CReadBuffer);
                gpioRegister = i2CReadBuffer[0];
                i2cMCP23008.WriteRead(new byte[] { MCP23008_OLAT }, i2CReadBuffer);
                olatRegister = i2CReadBuffer[0];

                // Konfiguracja PIN-a z laserem na 1; reszta bez zmian
                olatRegister |= LED_GPIO_PIN;
                i2CWriteBuffer = new byte[] { MCP23008_OLAT, olatRegister };
                i2cMCP23008.Write(i2CWriteBuffer);

                bitMask = (byte)(0xFF ^ LED_GPIO_PIN); // Tylko nasz PIN będzie miał maskę 0 - reszta będzie równa 1, co spowoduje że nie zmienią wartości
                iodirRegister &= bitMask;
                i2CWriteBuffer = new byte[] { MCP23008_IODIR, iodirRegister };
                i2cMCP23008.Write(i2CWriteBuffer);

            } catch (Exception e) {
                return;
            }


            //Komunikacja z Arduino
            byte[] wbuffer = new byte[] { 1, 2, 3, 4, 5, 6 };
            byte[] rbuffer = new byte[2];
			//Wysłanie liczb do dodania
            i2cArduino.Write(wbuffer);
            await Task.Delay(1000);
			//Odczytanie wyniku
            var result = i2cArduino.ReadPartial(rbuffer);
            //Wyświetlenie
			int sum = (((int)rbuffer[1]) << 8) + (int)rbuffer[0];
            Debug.WriteLine($"Suma:{sum}");

            //Błyskanie laserem co sekundę
            m_timer = new DispatcherTimer();
            m_timer.Interval = TimeSpan.FromMilliseconds(1000);
            m_timer.Tick += timer_Tick;
            m_timer.Start();

			//Zapis "trójkąta" do PCF8591 (w pętli, nieskończonej)
            await WritePCF8591();
        }
        private async Task WritePCF8591() {
            byte[] readBuf = new byte[1];
            await Task.Run(() => {
                while (true) {
                    for (int i = 0; i <= 255; i++) {
                        i2cPCF8591.Write(new byte[] { 0x40, (byte)i });//Włączamy DAC
                    }
                    for (int i = 255; i >= 0; i--) {
                        i2cPCF8591.Write(new byte[] { 0x40, (byte)i });//Włączamy DAC
                    }
                }
            }).AsAsyncAction();
        }

        private void timer_Tick(object sender, object e) {
            if ((olatRegister & LED_GPIO_PIN) == 0) {
                olatRegister |= LED_GPIO_PIN;
                i2cMCP23008.Write(new byte[] { MCP23008_OLAT, olatRegister });
            } else {
                byte bitMask = (byte)(0xFF ^ LED_GPIO_PIN);
                olatRegister &= bitMask;
                i2cMCP23008.Write(new byte[] { MCP23008_OLAT, olatRegister });
            }
        }
    }
}
