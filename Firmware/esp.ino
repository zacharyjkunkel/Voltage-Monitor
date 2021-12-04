//The built in Arduino SPI library
#include <SPI.h>


#define CSEL 13      // Chip Select (IO13) //was 20
#define CSEL2 15     // Chip Select 2 (IO15) //was 21

#define DATAOUT 33   // MOSI (SD1)
#define DATAIN 32    // MISO (SD0)
#define DCLK 31      // Clock (CLK)


//SPIQ = MISO = data in = IO19
//SPID = MOSI = data out = IO23
//SPICLK = IO18


unsigned int readvalue; //the 16 bit voltage value
double voltage = 6.25;  //the voltage range of the adc, only needed here if not using the c# application
unsigned int cycle = 0; //one cycle is reading through all 16 cells once
int currentCH = 0;      //the current channel that is being read numbered 0 - 15

//the 4 bytes used in an spi transfer
int start = 0;
int byteupper = 0;
int bytelower = 0;
int remains = 0;

//used for testing timing locally
unsigned long oldtime = 0;
unsigned long currentTime = 0;

//if testing locally, to print calculate the end voltage
double voltageValue = 0;


void setup(){ 

  //set pin modes 
  pinMode(CSEL, OUTPUT);
  pinMode(CSEL2, OUTPUT); 

  //initialize device 
  digitalWrite(CSEL, HIGH); 
  digitalWrite(CSEL2, HIGH);

  //setup SPI indo
  SPI.setClockDivider( SPI_CLOCK_DIV16 );
  SPI.setBitOrder( MSBFIRST );
  SPI.setDataMode( SPI_MODE0 );
  SPI.begin();

  //The baud rate determines how fast bits are being sent through the serial port
  Serial.begin(300000); //115200 38400 300000


  //setting up the first adc to use internal clock mode
  //initializing clock mode
  digitalWrite (CSEL, LOW);
  
  //setup adc to internal clock mode
  byte controlbyte = B10000110;
  SPI.transfer(controlbyte);
  
  //cycle through rest of processing cycle
  SPI.transfer(0);
  SPI.transfer(0);
  SPI.transfer(0);
  
  //disable adc
  digitalWrite (CSEL, HIGH);

  
  //setting up the second adc to use internal clock mode
  digitalWrite (CSEL2, LOW);
  
  //setup adc to internal clock mode
  controlbyte = B10000110;
  SPI.transfer(controlbyte);
  
  //cycle through rest of processing cycle
  SPI.transfer(0);
  SPI.transfer(0);
  SPI.transfer(0);
  
  //disable adc
  digitalWrite (CSEL2, HIGH);

  Serial.println("");
  Serial.println(micros());
  Serial.println("StartReading"); //tells the C# program to begin processing the serial port
}


void loop() {
  currentCH = 0;

  //Serial.print("Cycle: ");
  //Serial.println(cycle);
  
  int CH0 = read_adc(B10000100, 0); //S:A2:A1:A0:DC:SB:PD1:PD0, 
  int CH1 = read_adc(B10010100, 0);
  int CH2 = read_adc(B10100100, 0);
  int CH3 = read_adc(B10110100, 0);
  int CH4 = read_adc(B11000100, 0);
  int CH5 = read_adc(B11010100, 0);
  int CH6 = read_adc(B11100100, 0);
  int CH7 = read_adc(B11110100, 0);
  
  int CH8 = read_adc(B10000100, 1);
  int CH9 = read_adc(B10010100, 1);
  int CH10 = read_adc(B10100100, 1);
  int CH11 = read_adc(B10110100, 1);
  int CH12 = read_adc(B11000100, 1);
  int CH13 = read_adc(B11010100, 1);
  int CH14 = read_adc(B11100100, 1);
  int CH15 = read_adc(B11110100, 1);

  cycle++;
  //Serial.println("");
}

//takes in a control byte that contains info on which channel to read
//as well as which ADC to read from
unsigned int read_adc(byte controlbyte, int cs){
 
  //Choose which adc to read
  if (cs){
    digitalWrite(CSEL2, LOW); //csel2
  } else {
    digitalWrite(CSEL, LOW);  //csel
  }

    
  //choose channel and setup
  start = SPI.transfer(controlbyte);//controlbyte
  
  //upper 8 bits of result;
  byteupper = SPI.transfer(0);
  
  //lower 8 bits of result
  bytelower = SPI.transfer(0);

  //4th cycle to give adc time to reset
  //remains = SPI.transfer(0);

  //disable both adcs
  digitalWrite(CSEL, HIGH);
  digitalWrite(CSEL2, HIGH);


  //simulating values from ADC
  //byteupper = random(0, 255);
  //bytelower = random(0, 255);
  

  //combining both bytes into one value
  readvalue = (byteupper << 8) + bytelower; //integer between 0-65535
  
  //calculating the final voltage if debugging locally
  //voltageValue = (((double)readvalue) / 65535)  * voltage; // - 32767 




  ////////// OUTPUT Options //////////


  // Used to debug locally
  //Serial.print(micros()); //millis
  /*Serial.print(" CH");
  Serial.print(currentCH);
  Serial.print(" Voltage: ");
  Serial.print(readvalue);
  Serial.print("/65535 or in V: ");
  Serial.print(voltageValue);
  Serial.print("\n");*/
  //Serial.print(micros() - oldtime);
  //oldtime = micros();


  //Used to send in byte format, much faster this way
  //gets the current time to be used for timing purposes
  currentTime = micros();

  byte buf[10];
  buf[0] = cycle;                 //2 byte cycle
  buf[1] = (cycle >> 8);
  buf[2] = currentTime;           //4 byte current time
  buf[3] = (currentTime >> 8);
  buf[4] = (currentTime >> 16);
  buf[5] = (currentTime >> 24);
  buf[6] = currentCH;             //2 byte current channel
  buf[7] = (currentCH >> 8);
  buf[8] = (readvalue);           //2 byte  readvalue
  buf[9] = (readvalue >> 8);      //        readvalue >> 8
  //while (Serial.availableForWrite() <= 20) {}
  Serial.write(buf, 10);




  // writing bytes one at a time, slightly more inefficent than the buffer method
  //Serial.println(micros() - oldtime);
  //oldtime = micros();
  
  //Serial.write(cycle);      //2 bytes unsigned int
  //Serial.write(currentTime);   //4 bytes unsigned long
  //Serial.write(currentCH);  //2 bytes int
  //Serial.write(readvalue);  //2 bytes int
  
  /*//Serial.write("\n ");
  //Serial.print(micros() - oldtime);
  //Serial.write(" ");
  //oldtime = micros();*/




  // Sending information over serial by writing the actual characters over, much slower
  /*Serial.print(cycle);
  Serial.write(" ");
  Serial.print(micros()); //millis()
  Serial.write(" ");
  Serial.print(currentCH);
  Serial.write(" ");
  Serial.print(readvalue);
  Serial.write("\n");*/



  /*Serial.print(micros() - oldtime);
  oldtime = micros();
  Serial.write(" ");*/

  currentCH++;

  return(0);
}