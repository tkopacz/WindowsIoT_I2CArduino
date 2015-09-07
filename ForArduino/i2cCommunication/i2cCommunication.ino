//Demo For Arduino - I2C communication, add 2 numbers sended by Windows IoT (RPI2 in this case
)
#include <Wire.h>
byte x = 0;
byte arr[2];

void setup() {
  arr[0] = 1; //Tu będzie int z sumą
  arr[1] = 2;
  Serial.begin(9600);
  Wire.onReceive(receiveEvent); //Zgłaszane przy wysyłaniu (nadawca: Write)
  Wire.onRequest(requestEvent); //Zgłaszane przy żądaniu odczytu (nadawca: Read)
  Wire.begin(17); //Jesteśmy slave numer 17
  Serial.println("START");
}

void loop()
{
  //Nic
}

/*
 * Sprawdzać kabelki
 * RPI2: 3.3V 
 * RPI2: SDA1 -> Uno: A4 (zielony)
 * RPI2: SCL1 -> Uno: A5 (biały)
 * GND (czarny)
 */
void receiveEvent(int howMany) {  //Odebranie I2C, howMany - ile bajtów
    int sum=0;
    while (Wire.available() > 0){
        byte b = Wire.read();
        Serial.print(b, DEC);
        sum+=b;
    }
    arr[0] = sum & 0xFF;
    arr[1] = (sum>>8)& 0xFF; 
}

void requestEvent() {
    Serial.println("RESPONSE");
    Wire.write(arr,2);
}


