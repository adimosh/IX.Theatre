# DMX Client

The DMX client is a console-based application that listens to a serial port of your choice for
data coming in from DMX channels. It is supposed to be used alongside an Arduino Leonardo, which
reads the DMX data and feeds it through the serial port.

The client is given a set of channels to monitor, and a set of movie files to play - one per each
channel defined. Once data is received from the DMX master, the channel whose value is the highest
will have its movie played in a continuous loop

In order to see the Arduino part of the code, please go to [its repository
folder](https://github.com/adimosh/IX.Theatre/tree/dmxclient-0.1.0/DMX/Arduino/leonardo_dmx).

## Hardware setup

The hardware setup involves:

- an [Arduino Leonardo](https://docs.arduino.cc/hardware/leonardo/)
- A DMX shield for it (preferably a MAX485-based board)
- A [Raspberry Pi](https://www.raspberrypi.com/) (or anything able to run a .NET 9-based application
and connect to the Leonardo's USB port)
- (on Linux) the `libvlc` and `vlc` packages installed
- (on Windows) [VLC](https://www.videolan.org/vlc/) installed

## Software tool use

In order to use the application, the following format is used:

```
DmxClient.exe s:COM2 c:1 c:2 "f:D:\someFolder\1.mp4" "f:D:\someFolder\2.mp4" b
```

The arguments are as follows:

- s:[port name] is the name of the serial port to listen on
- c:[channel ID] is the ID of the DMX channel to monitor - any number of channels can be supported,
but they must be listed ( :construction: Temporary: a maximum list of 10 channels can be defined in the pre-release
versions, in order to ease internal testing. This limit will be completely removed as the final release version is
approaching :construction: )
- f:[path] is the path of files to play when the maximum value is on its respective channel - each
defined channel must have a file defined, and the order in wihch files are associated to channels
are the order in which the channels and files are defined
- b (optional) specifies that the channel value should influence brightness instead of contrast (which
is the default if you do not specify this argument

The above example is for Windows, but the arguments are the same and work the same way for Linux.