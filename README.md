# Voltage-Monitor
This was a 2 person (originally 4 but 2 people left and the project was scaled down) Senior Design project to create a device that monitors the voltage of 16 battery cells using SPI and communicates with a host PC via a serial port. I was primarily software and firmware while my partner was hardware.  

I used an ESP32 running Arduino to communicate with 2 Analog to Digital Converters (ADS8344) via SPI. The ESP32 would then send the voltage information to a host PC over USB through a serial port. On the host PC I had a C# program monitoring the serial port to do math on and format the voltage data. The data was the displayed in the command prompt in real time and/or logged in a csv or txt file.


Output in command prompt:  
![output](https://user-images.githubusercontent.com/95504904/144691797-29478b50-f807-478b-a83d-1c2970a0043e.png)


# Notes
- I used the built in Arduino SPI library
- The final project ended up not working because of issues with our PCB, but we had a breadboard prototype that was functional
