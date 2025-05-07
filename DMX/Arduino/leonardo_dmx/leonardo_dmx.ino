#include <DMXSerial.h>
#include <Keyboard.h>

// Initialization parameters
#define KEYSTROKE_DELAY 50   // How much to delay between checks

// Channels - please make sure that the arrays are THE SAME LENGTH and at least 2 in size
int channels[10];
int previousValues[] = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
int maxChannels;
bool initialized = false;

// Code starts here
void setup()
{
  // Initialize the DMX serial receiver
  DMXSerial.init(DMXReceiver);

  // Initialize serial port
  Serial.begin(9600);
  while (!Serial)
  {
    ;
  }
}

void loop()
{
  if (!initialized)
  {
    // Initialize the parameters
    initialize_sequence();
  }

  if (Serial.available() > 0)
  {
    // The serial port has most likely requested reinitialization
    for (int i = 0; i < 10; i++)
    {
      previousValues[i] = -1;
    }
    initialized = false;
    return;
  }

  // Read channel values
  for (int i = 0; i < maxChannels; i++)
  {
    int channel = channels[i];
    int previousValue = previousValues[i];
    int channelValue = DMXSerial.read(channel);
    if (channelValue == previousValue)
    {
      continue;
    }
    previousValues[i] = channelValue;

    char buffer[10];
    sprintf(buffer, "%d:%d;", channel, channelValue);
    Serial.print(buffer);
  }

  // Wait for the keystrokle delay, so as to not overload comms
  delay(KEYSTROKE_DELAY);
}

void initialize_sequence()
{
  // Initialize protocol
  String input = "";
  String expectedString = "Start";
  while (!expectedString.equalsIgnoreCase(input))
  {
    input = Serial.readStringUntil(';');
    input.trim();
  }
  Serial.print("Go start;");

  int channelIndex = 0;

  input = "";
  while (input != "Channel complete" && channelIndex < 10)
  {
    // Configure channels
    if (input.length() != 0)
    {
      int channelInput = input.toInt();
      if (channelInput != 0)
      {
        channels[channelIndex] = channelInput;
        channelIndex++;

        Serial.print("Channel OK;");
      }
    }

    input = Serial.readStringUntil(';');
    input.trim();
  }

  maxChannels = channelIndex;
  Serial.print("Channel complete OK;");

  initialized =  true;
}