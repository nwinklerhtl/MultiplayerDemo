#include <WiFi.h>
#include <ArduinoWebsockets.h>
#include <Adafruit_NeoPixel.h>
#include <ArduinoJson.h>

using namespace websockets;

#define LED_PIN1 13
#define LED_PIN2 14
#define LED_COUNT 20

const char* ssid = "OpenDays_SYTD";
const char* password = "HTLDoesIT!";
const char* wsUrl = "ws://192.168.0.7:5137/led";

Adafruit_NeoPixel strip1(LED_COUNT, LED_PIN1, NEO_GRB + NEO_KHZ800);
Adafruit_NeoPixel strip2(LED_COUNT, LED_PIN2, NEO_GRB + NEO_KHZ800);

WebsocketsClient client;

// Game state
int score1 = 0, score2 = 0;
bool flash1 = false, flash2 = false;
unsigned long flashUntil1 = 0, flashUntil2 = 0;

// --- Forward declarations (needed for C++)
void onMessageCallback(WebsocketsMessage msg);
void showScore(Adafruit_NeoPixel &strip, int score, bool flash, unsigned long &flashUntil);

void setup() {
  Serial.begin(115200);
  strip1.begin();
  strip2.begin();
  strip1.show();
  strip2.show();

  WiFi.begin(ssid, password);
  Serial.print("Connecting to WiFi");
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nConnected.");
  Serial.println(WiFi.localIP());

  delay(1000);
  client.onMessage(onMessageCallback);  

  if (client.connect(wsUrl)) {
    Serial.println("Connected to WebSocket server.");
  } else {
    Serial.println("WebSocket connection failed!");
  }
}

void loop() {
  client.poll();

  // Visualize scores (0–20 LEDs)
  showScore(strip1, score1, flash1, flashUntil1);
  showScore(strip2, score2, flash2, flashUntil2);
}

void onMessageCallback(WebsocketsMessage msg) {
  String payload = msg.data();
  Serial.println("MSG: " + payload);

  JsonDocument doc;
  DeserializationError err = deserializeJson(doc, payload);
  if (err) {
    Serial.println("JSON parse failed");
    return;
  }

  score1 = doc["p1"] | 0;
  score2 = doc["p2"] | 0;

  flash1 = doc["flash1"] | false;
  flash2 = doc["flash2"] | false;

  if (flash1) flashUntil1 = millis() + 300;
  if (flash2) flashUntil2 = millis() + 300;
}

void showScore(Adafruit_NeoPixel &strip, int score, bool flash, unsigned long &flashUntil) {
  int activeLeds = constrain(map(score, 0, 50, 0, LED_COUNT), 0, LED_COUNT);
  bool flashing = millis() < flashUntil;

  for (int i = 0; i < LED_COUNT; i++) {
    if (i < activeLeds) {
      uint32_t color = flashing ? strip.Color(255, 255, 255)
                                : strip.Color(0, 150, 255);
      strip.setPixelColor(i, color);
    } else {
      strip.setPixelColor(i, 0);
    }
  }
  strip.show();
}
