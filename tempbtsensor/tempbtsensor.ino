#include <SPI.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <Adafruit_Sensor.h>
#include <DHT.h>
#include <DHT_U.h>
#include <SoftwareSerial.h>

#define SCREEN_WIDTH 128 // OLED display width, in pixels
#define SCREEN_HEIGHT 64 // OLED display height, in pixels

// Declaration for an SSD1306 display connected to I2C (SDA, SCL pins)
#define OLED_RESET     4 // Reset pin # (or -1 if sharing Arduino reset pin)
Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);

#define DHTPIN 4
#define DHTTYPE    DHT11
DHT_Unified dht(DHTPIN, DHTTYPE);

float temp, humidity;
  
#define BT_RX 2
#define BT_TX 3
SoftwareSerial ble(BT_RX, BT_TX);

void setupLCD()
{
  if(!display.begin(SSD1306_SWITCHCAPVCC, 0x3C)) { // Address for 128x64
    Serial.println(F("SSD1306 allocation failed"));
    for(;;); // Don't proceed, loop forever
  }
  
  display.cp437(true);
  display.clearDisplay();
  display.display();
}

void setupBluetooth()
{  
  ble.begin(9600);
  return;
  
  do
  {
  if (ble.available())
  {
    char c = ble.read();
    Serial.print(c);
    //return;
  }
  else
  {
    //Serial.print("BLE not available");
    //Serial.println(++i);
    //delay(1000);
  }
  } while (true);
}

void setup() {
  Serial.begin(9600);
  setupBluetooth();
  
  setupLCD();
  dht.begin();
}

void loop() {
  dhtRead();
  render();
  btSend();
  delay(1000);
}

void dhtRead()
{
  sensors_event_t event;
  dht.temperature().getEvent(&event);
  temp = event.temperature;
  dht.humidity().getEvent(&event);
  humidity = event.relative_humidity;
}

void render()
{
  display.clearDisplay();
  display.setTextSize(1.5f);
  display.setTextColor(SSD1306_WHITE);
  display.setCursor(0, 0);

  
  display.print(F("Temp: "));
  display.print(temp);
  display.println(F("C"));

  display.print(F("Humidity: "));
  display.print(humidity);
  display.println(F("%"));

  display.display();
}

void btSend()
{
  ble.write("T:");
  ble.write(String(temp).c_str());
  ble.write(",H:");
  ble.write(String(humidity).c_str());
}
