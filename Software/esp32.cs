using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace ReadESP32
{
    public class Program
    {
        static SerialPort serialPort;       //The Serial Port object
        static bool cont;                   //Checks for user input 'q' to quit program
        static bool done;
        static int outputType = 2;          //Choose to write to (0) CSV or (1) human readable or (2) console output
        static double adcVoltageRange = 6.25;  //Should be set to the Reference voltage input of the ADC
                                               //static Stopwatch stopWatch = new Stopwatch();

        Queue<SerialData> dataFromSerial = new Queue<SerialData>();
        Queue<OutputData> dataToFile = new Queue<OutputData>();


        public struct SerialData
        {
            public uint cycle;
            public uint currentus;
            public int channel;
            public int voltage;

            public SerialData(uint cycle, uint currentus, int channel, int voltage)
            {
                this.cycle = cycle;
                this.currentus = currentus;
                this.channel = channel;
                this.voltage = voltage;
            }
        }

        public struct OutputData
        {
            public uint cycle;
            public uint currentus;
            public int channel;
            public double finalVoltage;
            public uint timetaken;
            public uint allchtimetaken;
            public uint allchtime;
            public uint totaltime;

            public OutputData(uint cycle, uint currentus, int channel, double finalVoltage,
                uint timetaken, uint allchtimetaken, uint allchtime, uint totaltime)
            {
                this.cycle = cycle;
                this.currentus = currentus;
                this.channel = channel;
                this.finalVoltage = finalVoltage;
                this.timetaken = timetaken;
                this.allchtimetaken = allchtimetaken;
                this.allchtime = allchtime;
                this.totaltime = totaltime;
            }
        }



        public static void Main(string[] args)
        {
            Program A = new Program();
            A.RunProgram();
        }

        public void RunProgram()
        {
            //set up serial port
            serialPort = new SerialPort();
            serialPort.PortName = "COM3";
            serialPort.BaudRate = 300000; //115200 38400
            serialPort.ReadTimeout = 5000;
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;
            serialPort.ReadBufferSize = 10000000;

            Thread readThread = new Thread(ReadSerial);
            Thread processThread = new Thread(ProcessSerial);
            Thread writeThread = new Thread(WriteData);


            //shared between threads
            cont = true;
            done = false;
            //Queue<SerialData> dataFromSerial;// = new Queue<SerialData>();
            //Queue<OutputData> dataToFile;// = new Queue<OutputData>();


            //check to see if port is in use
            try
            {
                serialPort.Open();
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Serial Port currently in use. Please Close any other applications using COM3");
                if (Console.ReadLine() == "q")
                {
                    return;
                }
            }


            //waits for esp32 to get past booting nonsense info before reading the serial port
            Console.WriteLine("Waiting for Device to Start ...");
            bool checkForStart = true;
            while (checkForStart)
            {
                try
                {
                    string check = serialPort.ReadLine();
                    //Console.WriteLine(check);
                    if (check.Equals("StartReading\r"))
                    {
                        checkForStart = false;
                    }
                }
                catch (TimeoutException) { }
            }
            Console.WriteLine("Device Started ... Reading Now\n");


            //starts reading device
            readThread.Start();
            processThread.Start();
            writeThread.Start();

            //Checks to see if user wants to exit program
            while (cont)
            {
                {
                    if (Console.ReadLine() == "q")
                    {
                        cont = false;
                    }
                }
            }

            //close up the serial port and read thread
            readThread.Join();
            processThread.Join();
            writeThread.Join();
            serialPort.Close();
        }





        //Reads the data from the serial port and gives it to Process Serial
        public void ReadSerial()
        {
            //Thread.Sleep(100);
            byte[] buffer = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; //10 byte buffer

            uint cycle = 0;
            uint prevCycle = 0;
            uint currentus = 0;
            uint prevcus = 0;
            int channel = 0;
            int prevch = 0;
            int voltage = 0;
            int prevVoltage = 0;

            //Thread.Sleep(500);

            while (cont)
            {
                try
                {

                    //if not enough room in buffer
                    //Console.WriteLine("bytes left: " + serialPort.BytesToRead);
                    if (serialPort.BytesToRead < 10) Thread.Sleep(100);


                    //buffer[0] = 1;

                    serialPort.Read(buffer, 0, 10);
                    //Array.Reverse(buffer);

                    cycle = BitConverter.ToUInt16(buffer, 0); //new byte[] { buffer[0], buffer[1], buffer[2], buffer[3] }
                    currentus = BitConverter.ToUInt32(buffer, 2);
                    channel = BitConverter.ToInt16(buffer, 6);
                    voltage = BitConverter.ToInt16(buffer, 8);

                    /*Console.WriteLine(cycle);
                    Console.WriteLine(currentus);
                    Console.WriteLine(channel);
                    Console.WriteLine(voltage);
                    Console.WriteLine(" ");*/



                    if (channel > 15 || channel < 0)
                    {
                        //END PROGRAM
                        cont = false;

                        //Console.WriteLine(cycle);
                        //Console.WriteLine(currentus);
                        //Console.WriteLine(channel);
                        //Console.WriteLine(voltage);

                        Console.SetCursorPosition(0, 5);
                        Console.WriteLine("Writing to file ... Please Wait");
                        /*while (!done)
                        {
                            Console.SetCursorPosition(0, 6);
                            //Console.WriteLine("Remaining: " + dataToFile.Count);
                            Thread.Sleep(1000);
                        */
                        //cont = false;

                        Console.SetCursorPosition(0, 23);
                        Console.WriteLine("You may close the program.");

                        return;
                    }

                    /*if (Math.Abs(cycle - prevCycle) > 1)
                    {
                        cycle = prevCycle;
                        currentus = prevcus;
                        channel = prevch;
                        voltage = prevVoltage;
                    }*/


                    //put all of the values into the queue that is sent to Process Serial
                    lock (dataFromSerial)
                    {
                        dataFromSerial.Enqueue(new SerialData(cycle, currentus, channel, voltage));
                    }

                    prevCycle = cycle;
                    prevcus = currentus;
                    prevch = channel;
                    prevVoltage = voltage;
                }
                catch (TimeoutException) { }
            }
        }







        public void ProcessSerial()
        {
            uint lastus = 55178;    //These are both used to determine how long it takes to read from the ADC old: 55230
            uint last16us = 55178;  //55ms is consistently when the device begins reading from the ADC
            uint allchtime = 0;
            uint totaltime = 0;
            uint allchtimetaken = 0;

            while (cont)
            {
                //checks if the dataFromSerial is empty
                //Console.WriteLine("READINGS: " + dataFromSerial.Count);
                if (dataFromSerial.Count == 0)
                {
                    Thread.Sleep(50);
                    continue;
                }
                //grab the serial data from the queue from ReadSerial()
                SerialData data;
                lock (dataFromSerial)
                {
                    data = dataFromSerial.Dequeue();
                }



                uint currentus = data.currentus;
                int channel = data.channel;
                int voltage = data.voltage;

                //calculate how long it took to read that channel in μs
                uint timetaken = currentus - lastus;

                //calculate final voltage
                double finalVoltage = Math.Round((((double)voltage / 65535) * adcVoltageRange), 3);

                //the first channel of each cycle
                if (channel == 0)
                {
                    //gets the time taken to read 16 channels μ
                    allchtime = currentus;
                    allchtimetaken = allchtime - last16us;
                    last16us = allchtime;

                    //if not first time
                    if (last16us != 55178)
                    {
                        totaltime += allchtimetaken;
                    }
                }

                lastus = lastus + timetaken;

                int dog = dataToFile.Count;
                lock (dataToFile)
                {
                    dataToFile.Enqueue(new OutputData(data.cycle, currentus, channel, finalVoltage,
                        timetaken, allchtimetaken, allchtime, totaltime));
                }
            }
        }







        public void WriteData()
        {
            int count16 = 0;

            while (cont)
            {
                //checks if the dataFromSerial is empty
                if (dataToFile.Count == 0)
                {
                    //Thread.Sleep(1000);
                    continue;
                }
                OutputData data;
                lock (dataToFile)
                {
                    data = dataToFile.Dequeue();
                }

                uint cycle = data.cycle;
                uint currentus = data.currentus;
                int channel = data.channel;
                double finalVoltage = data.finalVoltage;
                uint timetaken = data.timetaken;
                uint allchtimetaken = data.allchtimetaken;
                uint allchtime = data.allchtime;
                uint totaltime = data.totaltime;


                if (outputType == 0)
                {
                    //write data in csv format
                    using (StreamWriter file = new StreamWriter(@"C:\Users\magna\Documents\CODE\ecen403\ESP32OUT.csv", true))
                    {
                        file.Write(cycle + "," + channel + "," + finalVoltage + "," + timetaken + "\n");
                    }
                }

                if (outputType == 1 || outputType == 2)
                {
                    //Writing to file in an easily readable format
                    using (StreamWriter file = new StreamWriter(@"C:\Users\magna\Documents\CODE\ecen403\ESP32OUT.txt", true))
                    {
                        //the first channel of each cycle
                        if (count16 == 0) //channel
                        {
                            if (currentus != 55178)
                            {
                                if (outputType == 1)
                                {
                                    file.Write("Time Taken for last 16 Channels: " + allchtimetaken + "μs" + "     \n");
                                    file.Write("Time since start " + (double)allchtime / 1000000 + "s" + "     \n\n");
                                }
                                else if (outputType == 2)
                                {
                                    Console.Write("Time Taken for last 16 Channels: " + allchtimetaken + "μs" + "     \n");
                                    Console.Write("Time Since Start: " + (double)allchtime / 1000000 + "s" + "     \n\n");
                                    //file.Write("Time Taken for last 16 Channels: " + allchtimetaken + "μs" + "     \n");
                                    //file.Write("Time since start " + (double)allchtime / 1000000 + "s" + "     \n\n");
                                    Console.SetCursorPosition(0, 3);
                                }
                            }

                            //printing the cycle number
                            if (outputType == 1)
                            {
                                file.Write("Cycle: " + cycle + "                                \n");
                            }
                            else if (outputType == 2)
                            {
                                Console.Write("Cycle: " + cycle + "                                \n");
                                //file.Write("Cycle: " + cycle + "                                \n");
                            }
                        }


                        if (outputType == 1)
                        {
                            file.Write("Channel: " + channel + "; Voltage: " + finalVoltage + " V" +
                                "; Time Taken: " + timetaken + "μs" + "     \n");
                        }
                        else if (outputType == 2)
                        {
                            Console.Write("Channel: " + channel.ToString().PadLeft(2, '0') + "; Voltage: " + finalVoltage.ToString("F3") + " V" +
                            "; Time Taken: " + timetaken + "μs" + "     \n");
                            //file.Write("Channel: " + channel + "; Voltage: " + finalVoltage + " V" +
                            //    "; Time Taken: " + timetaken + "μs" + "     \n");
                        }


                    }
                }
                count16++;
                if (count16 == 16) count16 = 0;
            }
        }
    }
}
