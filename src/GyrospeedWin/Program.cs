﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace GyrospeedWin {
    class Program {
        // TAP file header values
        const string TAP_FILE_MAGIC = "C64-TAPE-RAW";
        const byte TAP_FILE_VERSION = 1;
        const int TAP_FILE_HEADER_SIZE = 0x14;
        const int TAP_FILE_DATA_LENGTH_OFFSET = 0x10;

        // CBM pulse values
        const byte SHORT_PULSE = 0x2f;
        const byte MEDIUM_PULSE = 0x42;
        const byte LONG_PULSE = 0x56;

        // Gyrospeed turbo pulse values
        const byte GYROSPEED_BIT_OFF = 0x15;
        const byte GYROSPEED_BIT_ON = 0x2a;

        // The standard pilot lengths for the CBM and data headers
        const int NUM_PULSES_FOR_CBM_HEADER_PILOT = 0x6a00;
        const int NUM_PULSES_FOR_DATA_HEADER_PILOT = 0x1500;

        // The standard trailer lengths for the CBM and data headers
        const int NUM_PULSES_FOR_TRAILER = 0x4f;
        const int NUM_PULSES_FOR_REPEAT_TRAILER = 0x4e;

        // The standard sync chain markers for headers and repeat headers
        static readonly byte[] syncChain = new byte[] { 0x89, 0x88, 0x87, 0x86, 0x85, 0x84, 0x83, 0x82, 0x81 };
        static readonly byte[] syncRepeatChain = new byte[] { 0x09, 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 };

        // The number of clock cycles to stipulate in the TAP file when inserting a gap pause (silence) between blocks (roughly 330ms)
        const int NUM_CLOCK_CYCLES_FOR_GAP_PAUSE = 0x50000;

        // The number of clock cycles to stipulate in the TAP file when inserting the end pause (silence) (roughly 5 seconds)
        const int NUM_CLOCK_CYCLES_FOR_END_PAUSE = 0x4b2b20;

        // These are the characters which can be used in a BASIC string to clear the screen (CHR$(147)) and change the text colour to white (CHR$(5))
        // They are inserted at the start of the filename in the CBM header to alter the appearance of the found message
        static readonly byte[] clearScreenAndSetTextColourToWhite = new byte[] { 0x93, 0x05 };

        // The default BASIC start address
        const int BASIC_START_ADDRESS = 0x0801;

        // The BASIC token for the SYS command
        const byte BASIC_SYS_TOKEN = 0x9e;

        // The offset of the loading bars/border flashing effect routine in gyrospeed-header.prg
        const int LOADING_EFFECT_ROUTINE_OFFSET = 0xa6;

        // The XOR checksum for the data loaded by the Gyrospeed loader
        static byte gyroSpeedCheckSum = 0;

        // The filename of the CBM header containing the Gyrospeed loader code (built from gyrospeed-header.asm)
        const string GyrospeedHeaderFileName = "gyrospeed-header.prg";

        // The filename of the data header containing the Gyrospeed boot code (built from gyrospeed-boot.asm)
        const string GyrospeedBootFileName = "gyrospeed-boot.prg";

        static readonly Version version = Assembly.GetExecutingAssembly().GetName().Version;
        static readonly string versionString = $"GyrospeedWin v{version.Major}.{version.Minor} - StatMat November 2020";

        static int Main(string[] args) {
            try {
                // basic argument check
                if(args.Length == 0 || args.Length > 1 ||
                    args[0] == "-?" || args[0] == "/?" || args[0] == "-h" || args[0] == "/h" || args[0] == "-help" || args[0] == "/help" || args[0] == "--help") {
                    Console.WriteLine($"\n                  {versionString}");
                    Console.WriteLine("\nUsage: GyrospeedWin <crunched-prg-file> -or- <folder-containing-crunched-prgs>");
                    Console.WriteLine("\n             The crunched PRG files should have a BASIC SYS line");
                    Console.WriteLine("        at $0801 to run the program (e.g. crunched with Exomizer etc).");
                    Console.WriteLine("\n        PRG files can be loaded into the address range $0400 - $cfff.");
                    Console.WriteLine("\n      It is based on Gyrospeed written by Gary Saunders which was included");
                    Console.WriteLine("    as a type-in listing in the April 1988 issue of Your Commodore magazine.");
                    Environment.Exit(0);
                }

                var exePath = GetExecutingDirectory();

                if(!File.Exists(Path.Combine(exePath, GyrospeedHeaderFileName))) {
                    Console.WriteLine($"\nCannot find {GyrospeedHeaderFileName}!");
                    WaitForKeyAndExit();
                }

                if(!File.Exists(Path.Combine(exePath, GyrospeedBootFileName))) {
                    Console.WriteLine($"\nCannot find {GyrospeedBootFileName}!");
                    WaitForKeyAndExit();
                }

                var isFolder = Directory.Exists(args[0]);

                if(!isFolder &&
                    !File.Exists(args[0])) {
                    Console.WriteLine("\nParameter is neither a valid file or folder!");
                    WaitForKeyAndExit();
                }

                FileSystemInfo[] fileSysInfo;

                if(isFolder) {
                    var dirInfo = new DirectoryInfo(args[0]);
                    fileSysInfo = dirInfo.EnumerateFileSystemInfos("*.prg").Where(f => f is FileInfo).ToArray();
                }
                else {
                    fileSysInfo = new FileSystemInfo[] { new FileInfo(args[0]) };
                }

                Console.WriteLine("\nPlease enter desired loading effect:\n");
                Console.WriteLine("0 - Original                   5 - Medium Stripes");
                Console.WriteLine("1 - Original Double Height     6 - Thick Stripes");
                Console.WriteLine("2 - Freeload Style             7 - Black and White");
                Console.WriteLine("3 - Freeload Alt Style         8 - Jolly Stripes");
                Console.WriteLine("4 - Stripe Columns             9 - Mixed Up");
                Console.WriteLine("\nR - use a random effect " + (fileSysInfo.Length > 1 ? "for each PRG file" : "") + "\n");

                var loadingEffectNum = 0;
                ConsoleKeyInfo key;

                do {
                    key = Console.ReadKey(true);
                }
                while(key.KeyChar < '0' || key.KeyChar > '9' && char.ToLower(key.KeyChar) != 'r');

                var useRandomLoadingEffect = false;

                if(char.ToLower(key.KeyChar) != 'r') {
                    loadingEffectNum = key.KeyChar - '0';
                }
                else {
                    useRandomLoadingEffect = true;
                }

                var rnd = new Random(GetSeed());

                // Read in Gyrospeed boot routine file
                // This routine is called after the headers have been loaded as it hijacks the BASIC idle loop vector at $0302.
                // It then calls the loader code stored in the CBM header at offset $0351. It then finally starts
                // the program after the turbo load has finished by performing a BASIC RUN.
                var gyroBootBuf = File.ReadAllBytes(Path.Combine(exePath, GyrospeedBootFileName));

                foreach(var file in fileSysInfo) {
                    Console.Write($"Processing {file.Name} - ");

                    // Read in crunched PRG
                    var crunchedPrgBuf = File.ReadAllBytes(file.FullName);

                    // Read the load address
                    var crunchedPrgLoadAdress = (ushort)(crunchedPrgBuf[1] << 8 | crunchedPrgBuf[0]);

                    if(crunchedPrgLoadAdress < 0x0400 ||
                        crunchedPrgLoadAdress + crunchedPrgBuf.Length - 2 > 0xd000) {
                        Console.WriteLine("program must load into the address range $0400 - $cfff - aborting");
                        WaitForKeyAndExit();
                    }

                    // Find the BASIC SYS address
                    var sysAddress = -1;

                    // Only check for the BASIC SYS line if the PRG file load address is less than or equal to the BASIC start address ($0801)
                    if(crunchedPrgLoadAdress <= BASIC_START_ADDRESS) {
                        sysAddress = FindSys(crunchedPrgBuf, BASIC_START_ADDRESS - crunchedPrgLoadAdress);
                    }

                    if(sysAddress == -1) {
                        Console.WriteLine("couldn't locate BASIC SYS line at $0801 - aborting");
                        WaitForKeyAndExit();
                    }

                    Console.WriteLine($"found BASIC SYS line with jump address ${sysAddress:x4} ({sysAddress})");

                    // If the user has selected random effects, then select one
                    if(useRandomLoadingEffect) {
                        var randomLoadingEffectNum = 0;

                        do {
                            randomLoadingEffectNum = rnd.Next(0, 10);
                        }
                        while(randomLoadingEffectNum == loadingEffectNum) ;

                        loadingEffectNum = randomLoadingEffectNum;
                    }

                    byte[] loadingEffect = null;

                    switch(loadingEffectNum) {
                        case 0:
                            // Nothing to do as this is already embedded in the gyrospeed-header.prg file
                            break;
                        case 1:
                            loadingEffect = LoadingEffects.OriginalDoubleHeightLines;
                            break;
                        case 2:
                            loadingEffect = LoadingEffects.FreeLoad;
                            break;
                        case 3:
                            loadingEffect = LoadingEffects.FreeLoadAlternative;
                            break;
                        case 4:
                            loadingEffect = LoadingEffects.StripeColumns;
                            break;
                        case 5:
                            loadingEffect = LoadingEffects.MediumStripes;
                            break;
                        case 6:
                            loadingEffect = LoadingEffects.ThickStripes;
                            break;
                        case 7:
                            loadingEffect = LoadingEffects.BlackAndWhite;
                            break;
                        case 8:
                            loadingEffect = LoadingEffects.JollyStripes;
                            break;
                        case 9:
                            loadingEffect = LoadingEffects.MixedUp;
                            break;
                    }

                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);

                    // Just cheat and convert to uppercase so that it'll display correctly in PETSCII
                    // Also strip off the "-[ex]" suffix if present (i.e. if this is a crunched PRG from the OneLoad64 collection)
                    var fileNameUpper = fileNameWithoutExtension.ToUpper().Replace("-[EX]", "");

                    // Only allow a maximum of 14 characters, as these will be preceded by the two character codes to clear the screen and set the text colour
                    fileNameUpper = fileNameUpper.Length >= 14 ? fileNameUpper.ToUpper().Substring(0, 14) : fileNameUpper.ToUpper();

                    // Add the two character prefix to clear the screen and set the text colour to white
                    var fileNameBytes = clearScreenAndSetTextColourToWhite.Concat(Encoding.UTF8.GetBytes(fileNameUpper)).ToArray();

                    // Read in the CBM header containing the Gyrospeed loader
                    var cbmHeaderBuf = File.ReadAllBytes(Path.Combine(exePath, GyrospeedHeaderFileName));

                    // Insert the filename into the CBM header
                    Array.Copy(fileNameBytes, 0, cbmHeaderBuf, 7, fileNameBytes.Length);

                    // Overwrite the original loading effect if needed
                    if(loadingEffect != null) {
                        Array.Copy(loadingEffect, 0, cbmHeaderBuf, LOADING_EFFECT_ROUTINE_OFFSET, loadingEffect.Length);
                    }

                    // Calculate the CBM header checksum
                    // Start at index 2 because first two bytes are the load address ($033c)
                    // Note: bytes 4, 5, 6 and 7 specify the start and end address of the Gyrospeed boot routine (e.g. $02bc and $0304)
                    var cbmHeaderBufCheckSum = CalcXorChecksum(cbmHeaderBuf, 2);

                    // Calculate the Gyrospeed boot routine checksum
                    // Start at index 2 because first two bytes are the load address ($02bc)
                    var gyroBootBufCheckSum = CalcXorChecksum(gyroBootBuf, 2);

                    using(var tapStream = File.Open(Path.Combine(exePath, $"{fileNameWithoutExtension}.tap"), FileMode.Create))
                    using(var binWriter = new BinaryWriter(tapStream)) {

                        // Write the TAP file header
                        binWriter.Write(TAP_FILE_MAGIC.ToCharArray());
                        binWriter.Write(TAP_FILE_VERSION);

                        // Reserved
                        binWriter.Write((byte)0x00);
                        binWriter.Write((byte)0x00);
                        binWriter.Write((byte)0x00);

                        // Data size - zero for now - will get updated after the complete TAP file has been written
                        binWriter.Write(0x00);

                        // Write CBM header pilot
                        WritePilotOrTrailer(binWriter, NUM_PULSES_FOR_CBM_HEADER_PILOT);

                        // Write sync chain
                        WriteBytes(binWriter, syncChain, 0);

                        // Write CBM header
                        WriteBytes(binWriter, cbmHeaderBuf, 2);

                        // Write CBM header checksum
                        TapeWriteByte(binWriter, cbmHeaderBufCheckSum);

                        // Write end of data marker
                        WriteEndOfDataMarker(binWriter);

                        // Write CBM header trailer
                        WritePilotOrTrailer(binWriter, NUM_PULSES_FOR_TRAILER);

                        // Write repeat sync chain
                        WriteBytes(binWriter, syncRepeatChain, 0);

                        // Write repeated CBM header
                        WriteBytes(binWriter, cbmHeaderBuf, 2);

                        // Write repeated CBM header checksum
                        TapeWriteByte(binWriter, cbmHeaderBufCheckSum);

                        // Write end of data marker
                        WriteEndOfDataMarker(binWriter);

                        // Write repeated CBM header trailer
                        WritePilotOrTrailer(binWriter, NUM_PULSES_FOR_REPEAT_TRAILER);

                        // Write gap pause (silence)
                        WritePause(binWriter, NUM_CLOCK_CYCLES_FOR_GAP_PAUSE);

                        // Write data header pilot
                        WritePilotOrTrailer(binWriter, NUM_PULSES_FOR_DATA_HEADER_PILOT);

                        // Write sync chain
                        WriteBytes(binWriter, syncChain, 0);

                        // Write data header (Gyrospeed boot code)
                        WriteBytes(binWriter, gyroBootBuf, 2);

                        // Write data header (Gyrospeed boot code) checksum
                        TapeWriteByte(binWriter, gyroBootBufCheckSum);

                        // Write end of data marker
                        WriteEndOfDataMarker(binWriter);

                        // Write data header trailer
                        WritePilotOrTrailer(binWriter, NUM_PULSES_FOR_TRAILER);

                        // Write repeat sync chain
                        WriteBytes(binWriter, syncRepeatChain, 0);

                        // Write repeated data header (Gyrospeed boot code)
                        WriteBytes(binWriter, gyroBootBuf, 2);

                        // Write repeated data header (Gyrospeed boot code) checksum
                        TapeWriteByte(binWriter, gyroBootBufCheckSum);

                        // Write end of data marker
                        WriteEndOfDataMarker(binWriter);

                        // Write repeated data header trailer
                        WritePilotOrTrailer(binWriter, NUM_PULSES_FOR_REPEAT_TRAILER);

                        // Write gap pause (silence)
                        WritePause(binWriter, NUM_CLOCK_CYCLES_FOR_GAP_PAUSE);

                        // Write Gyrospeed turbo pilot/sync-sequence
                        // $40 x $40 followed by a single $5a
                        WriteBytes(binWriter, 0x40, 0x40, true);
                        TapeWriteByte(binWriter, 0x5a, true);

                        // Write lo-byte, hi-byte of the crunched programs load address
                        // Write lo-byte, hi-byte of the pointer to beginning of the BASIC variable area (end of crunched program plus 1)
                        var headerBuf = new byte[] {
                            (byte)(crunchedPrgLoadAdress & 0xff),
                            (byte)((crunchedPrgLoadAdress >> 8) & 0xff),
                            (byte)(crunchedPrgBuf.Length - 2 + crunchedPrgLoadAdress),
                            (byte)((crunchedPrgBuf.Length - 2 + crunchedPrgLoadAdress) >> 8)
                        };

                        // Write out the header data bits
                        WriteBytes(binWriter, headerBuf, 0, true);

                        // Now that we're about to start writing the actual data, initialise the checksum byte to zero
                        gyroSpeedCheckSum = 0;

                        // Write out the PRG data bits
                        // Start at index 2 because first two bytes are the load address
                        WriteBytes(binWriter, crunchedPrgBuf, 2, true);

                        // Write the checksum byte (basically the result of XOR'ing all of the PRG bytes together)
                        TapeWriteByte(binWriter, gyroSpeedCheckSum, true);

                        // Write end pause (silence)
                        WritePause(binWriter, NUM_CLOCK_CYCLES_FOR_END_PAUSE);

                        // Update the file length field in the TAP file header
                        var fileLength = tapStream.Position - TAP_FILE_HEADER_SIZE;
                        tapStream.Seek(TAP_FILE_DATA_LENGTH_OFFSET, SeekOrigin.Begin);
                        binWriter.Write((int)fileLength);
                    }
                }

                return 0;
            }
            catch(Exception ex) {
                Console.WriteLine($"\n{ex.Message}");
                WaitForKeyAndExit();
            }

            return -1;
        }

        static void WaitForKeyAndExit() {
            Console.ReadKey(true);
            Environment.Exit(-1);
        }

        static byte CalcXorChecksum(byte[] buf, int startIndex) {
            if(startIndex > buf.Length - 1) {
                throw new ArgumentException("Start index is outside the bounds of the array.");
            }

            byte checksum = 0;

            for(var i = startIndex; i < buf.Length; i++) {
                checksum ^= buf[i];
            }

            return checksum;
        }

        static void TapeWriteByte(BinaryWriter binWriter, byte data, bool useGyrospeedTurbo = false) {
            // Write Gyrospeed encoded pulse
            if(useGyrospeedTurbo) {
                for(var j = 7; j > -1; j--) {
                    // bit = 1
                    if((data & (1 << j)) != 0) {
                        binWriter.Write(GYROSPEED_BIT_ON);
                    }
                    // bit = 0
                    else {
                        binWriter.Write(GYROSPEED_BIT_OFF);
                    }
                }

                // Keep a track of all the data bytes XOR'd together
                gyroSpeedCheckSum ^= data;
            }
            // Write standard CBM pulses
            else {
                // New data marker
                binWriter.Write(LONG_PULSE);
                binWriter.Write(MEDIUM_PULSE);

                bool checkbit = true;
                int bits;

                for(bits = 0; bits <= 7; bits++) {
                    // bit = 1
                    if((data & (1 << bits)) != 0) {
                        checkbit = !checkbit;

                        binWriter.Write(MEDIUM_PULSE);
                        binWriter.Write(SHORT_PULSE);
                    }
                    // bit = 0
                    else {
                        binWriter.Write(SHORT_PULSE);
                        binWriter.Write(MEDIUM_PULSE);
                    }
                }

                // Write the checkbit (parity)
                if(checkbit) {
                    binWriter.Write(MEDIUM_PULSE);
                    binWriter.Write(SHORT_PULSE);
                }
                else {
                    binWriter.Write(SHORT_PULSE);
                    binWriter.Write(MEDIUM_PULSE);
                }
            }
        }

        static void WriteBytes(BinaryWriter binWriter, byte data, int length, bool useGyrospeedTurbo = false) {
            WriteBytes(binWriter, Enumerable.Repeat(data, length).ToArray(), 0, useGyrospeedTurbo);
        }

        static void WriteBytes(BinaryWriter binWriter, byte[] data, int start, bool useGyrospeedTurbo = false) {
            for(var i = start; i < data.Length; i++) {
                TapeWriteByte(binWriter, data[i], useGyrospeedTurbo);
            }
        }

        static void WritePilotOrTrailer(BinaryWriter binWriter, int length) {
            while(length-- > 0) {
                binWriter.Write(SHORT_PULSE);
            }
        }

        static void WritePause(BinaryWriter binWriter, int clockCycles) {
            // Pauses of greater than 2048 clock cycles in the TAP v1 format start with a zero and are followed by an interval expressed in 3 bytes
            // So this will write a zero followed by a three byte integer in LOW/HIGH format representing the number of clock cycles
            binWriter.Write((byte)0x00);
            binWriter.Write((byte)(clockCycles & 0xff));
            binWriter.Write((byte)((clockCycles >> 8) & 0xff));
            binWriter.Write((byte)((clockCycles >> 16) & 0xff));
        }

        static void WriteEndOfDataMarker(BinaryWriter binWriter) {
            binWriter.Write(LONG_PULSE);
            binWriter.Write(SHORT_PULSE);
        }

        static int FindSys(byte[] buf, int basicStartOffset) {
            // Skip load address, link and line number
            var i = basicStartOffset + 6;

            // We only take a SYS statement from the start of the first line, as other more complicated BASIC start routines are likely to fail
            if(buf[i] == BASIC_SYS_TOKEN) {
                i++;

                // Exit loop at end of line
                while((i - basicStartOffset) < 1000 && buf[i] != '\0') {
                    var c = buf[i];

                    // Skip spaces and left parenthesis, if any
                    if(!" (".Contains((char)c)) {
                        var pos = i;

                        // Find the number of digits
                        while(buf[pos] >= 0x30 && buf[pos] <= 0x39) {
                            pos++;
                        }

                        var end = 0;

                        // Find the line end, as it may have trailing text (e.g. cruncher name etc)
                        while(buf[pos + end] != 0) {
                            end++;
                        }

                        var t = new ASCIIEncoding();

                        var addressString = t.GetString(buf, i, pos - i);

                        if(addressString.Length > 0) {
                            return int.Parse(addressString);
                        }
                    }

                    i++;
                }
            }

            return -1;
        }

        static int GetSeed() {
            using(var rng = new RNGCryptoServiceProvider()) {
                var intBytes = new byte[4];
                rng.GetBytes(intBytes);
                return BitConverter.ToInt32(intBytes, 0);
            }
        }

        static string GetExecutingDirectory() {
            var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
            return new FileInfo(location.AbsolutePath).Directory.FullName;
        }
    }
}