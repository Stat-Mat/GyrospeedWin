using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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

        // The standard pilot lengths for the CBM and data headers
        const int NUM_PULSES_FOR_CBM_HEADER_PILOT = 0x6a00;
        const int NUM_PULSES_FOR_DATA_HEADER_PILOT = 0x1500;

        // The standard trailer lengths for the CBM and data headers
        const int NUM_PULSES_FOR_TRAILER = 0x4f;
        const int NUM_PULSES_FOR_REPEAT_TRAILER = 0x4e;

        // The standard sync chain markers for headers and repeat headers
        static readonly byte[] syncChain = new byte[] { 0x89, 0x88, 0x87, 0x86, 0x85, 0x84, 0x83, 0x82, 0x81 };
        static readonly byte[] syncRepeatChain = new byte[] { 0x09, 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 };

        // The max filename length in the CBM header
        const int CBM_HEADER_MAX_FILENAME_LENGTH = 0x10;

        // This is the character code which can be used to clear the screen (CHR$(147))
        // It is inserted at the start of the filename in the CBM header to alter the appearance of
        // the found message, along with any colour code the user selects from the list below
        const byte CHR_CODE_CLEAR_SCREEN = 0x93;

        // The mappings for the character codes to change text colour
        static readonly byte[] chrColourMapping = new byte[] {
            0x90, // black
            0x05, // white
            0x1c, // red
            0x9f, // cyan
            0x9c, // purple
            0x1e, // green
            0x9e, // yellow
            0x81, // orange
            0x95, // brown
            0x96, // light red
            0x97, // dark grey
            0x98, // grey
            0x99, // light green
            0x9a, // light blue
            0x9b  // light grey
        };

        // The CPU frequency on a PAL machine is derived from the PAL C64 VIC-II colour clock frequency of 17.734475 MHz / 18
        const double PAL_CPU_FREQUENCY_IN_HZ = 985248;

        // The number of clock cycles to stipulate in the TAP file when inserting a gap pause (silence) between blocks (roughly 330ms on a PAL machine)
        const int NUM_CLOCK_CYCLES_FOR_GAP_PAUSE = 0x50000;

        // The number of clock cycles to stipulate in the TAP file when inserting the end pause (silence) (roughly 5 seconds on a PAL machine)
        const int NUM_CLOCK_CYCLES_FOR_END_PAUSE = 0x4b2b20;

        // The filename of the CBM header containing the Gyrospeed loader code (built from gyrospeed-header.asm)
        const string GYROSPEED_HEADER_FILENAME = "gyrospeed-header.prg";

        // The offset of the loading bars/border flashing effect routine in gyrospeed-header.prg
        const int GYROSPEED_HEADER_LOADING_EFFECT_ROUTINE_OFFSET = 0xa6;

        // The filename of the data header containing the Gyrospeed boot code (built from gyrospeed-boot.asm)
        const string GYROSPEED_BOOT_FILENAME = "gyrospeed-boot.prg";

        // Gyrospeed turbo pulse values
        const byte GYROSPEED_OFF_BIT = 0x15;
        const byte GYROSPEED_ON_BIT = 0x2a;

        // The default BASIC start address
        const int BASIC_START_ADDRESS = 0x0801;

        // The BASIC token for the SYS command
        const byte BASIC_SYS_TOKEN = 0x9e;

        static double prgFileLengthInSeconds = 0;

        // The XOR checksum for the data loaded by the Gyrospeed loader
        static byte gyroSpeedCheckSum = 0;

        static readonly Version version = Assembly.GetExecutingAssembly().GetName().Version;
        static readonly string versionString = $"GyrospeedWin v{version.Major}.{version.Minor} - Written by StatMat";

        static int Main(string[] args) {
            int loadingEffectNum = 0;
            int textColourNum = 0;
            int compilationTapeLength = 0;

            bool isFolder = false;
            bool buildCompilationTapFiles = false;
            bool buildIndividualTapFiles = false;
            bool useRandomLoadingEffect = false;
            bool useRandomTextColour = false;

            List<byte> chrCodePrefix = new List<byte>();

            string pathToWriteTapFiles = string.Empty;
            string exePath = GetExecutingDirectory();

            ConsoleKeyInfo key;
            Random rnd = new Random(GetSeed());
            PrgFile[] prgFiles = null;

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

                if(!File.Exists(Path.Combine(exePath, GYROSPEED_HEADER_FILENAME))) {
                    Console.WriteLine($"\nCannot find {GYROSPEED_HEADER_FILENAME}!");
                    WaitForKeyAndExit();
                }

                if(!File.Exists(Path.Combine(exePath, GYROSPEED_BOOT_FILENAME))) {
                    Console.WriteLine($"\nCannot find {GYROSPEED_BOOT_FILENAME}!");
                    WaitForKeyAndExit();
                }

                isFolder = Directory.Exists(args[0]);

                if(!isFolder &&
                    !File.Exists(args[0])) {
                    Console.WriteLine("\nParameter is neither a valid file or folder!");
                    WaitForKeyAndExit();
                }

                if(isFolder) {
                    var dirInfo = new DirectoryInfo(args[0]);

                    prgFiles = dirInfo.EnumerateFileSystemInfos("*.prg")
                    .Where(f => f is FileInfo)
                    .Select(
                        f => {
                            return new PrgFile {
                                Path = f.FullName,
                                Name = f.Name,
                                Size = ((FileInfo)f).Length
                            };
                        }
                    )
                    .ToArray();

                    if(prgFiles.Length == 0) {
                        Console.WriteLine("\nInput folder does not contain any PRG files!");
                        WaitForKeyAndExit();
                    }
                    else {
                        if(AskYesOrNo("Multiple PRG files detected in input folder. Would you like to build compilation TAP files?")) {
                            buildCompilationTapFiles = true;

                            // Keep asking for a length until it's a valid integer
                            do {
                                ClearConsoleAndWriteLine("\nPlease enter desired tape length in minutes (e.g. C60 = 60):");
                            }
                            while(!int.TryParse(Console.ReadLine(), out compilationTapeLength));

                            buildIndividualTapFiles = AskYesOrNo("Would you like to also build individual TAP files?");
                        }
                    }

                    pathToWriteTapFiles = Path.Combine(exePath, $"{dirInfo.Name}-TAPs");
                }
                else {
                    var fileInfo = new FileInfo(args[0]);

                    prgFiles = new PrgFile[] {
                        new PrgFile {
                            Path = fileInfo.FullName,
                            Name = fileInfo.Name,
                            Size = fileInfo.Length
                        }
                    };

                    pathToWriteTapFiles = $"{prgFiles[0].Path}-TAP";
                }

                // Create the output folder for the TAP file(s)
                Directory.CreateDirectory(pathToWriteTapFiles);

                ClearConsoleAndWriteLine("\nPlease choose desired loading effect:\n");
                Console.WriteLine("0 - Original                      5 - Medium Stripes");
                Console.WriteLine("1 - Original Double Height        6 - Thick Stripes (US Gold Style)");
                Console.WriteLine("2 - Freeload Style                7 - Black and White");
                Console.WriteLine("3 - Freeload Alt Style            8 - Jolly Stripes");
                Console.WriteLine("4 - Stripe Columns                9 - Mixed-Up (Rack It Style)");
                Console.WriteLine("A - Hi-Tec Stripe Columns         F - Gremlin Style (Alt. World Games)");
                Console.WriteLine("B - Black and Red Stripes         G - Firebird Black and Blue (Black Lamp Style)");
                Console.WriteLine("C - Flashing with Flatulence      H - Two shades of grey with noise");
                Console.WriteLine("D - Titus Black and Light Blue    I - Black and White Stripe Columns");
                Console.WriteLine("E - Cruncher AB Depack FX         J - It's a sin!");
                Console.WriteLine("\nR - use a random effect " + (prgFiles.Length > 1 ? "for each PRG file" : ""));

                // Ask user for their effect choice
                do {
                    key = Console.ReadKey(true);

                    if((key.KeyChar >= '0' && key.KeyChar <= '9') ||
                        (key.Key >= ConsoleKey.A && key.Key <= ConsoleKey.J) ||
                        key.Key == ConsoleKey.R) {
                        // We have a valid selection, so just break out of the infinite loop
                        break;
                    }
                }
                while(true);

                if(key.Key == ConsoleKey.R) {
                    useRandomLoadingEffect = true;
                }
                else {
                    if(key.KeyChar <= '9') {
                        loadingEffectNum = key.KeyChar - '0';
                    }
                    else {
                        loadingEffectNum = (key.Key - ConsoleKey.A) + 10;
                    }
                }

                // Ask user for their found message style choice
                ClearConsoleAndWriteLine("\nPlease choose desired found message style:\n");
                Console.WriteLine("0 - Standard (original)    1 - Clear screen");

                do {
                    key = Console.ReadKey(true);
                }
                while(key.KeyChar < '0' || key.KeyChar > '1');

                // If the user selected the clear screen found message style, then add the character code for it to the prefix
                if(key.KeyChar == '1') {
                    chrCodePrefix.Add(CHR_CODE_CLEAR_SCREEN);
                }

                // Ask user for their found message text colour choice
                ClearConsoleAndWriteLine("\nPlease choose desired found message text colour:\n");
                Console.WriteLine("D - Light Blue (original)\n");
                Console.WriteLine("0 - Black          5 - Green");
                Console.WriteLine("1 - White          6 - Yellow");
                Console.WriteLine("2 - Red            7 - Orange");
                Console.WriteLine("3 - Cyan           8 - Brown");
                Console.WriteLine("4 - Purple         9 - Light Red");
                Console.WriteLine("A - Dark Grey      C - Light Green");
                Console.WriteLine("B - Grey           E - Light Grey");
                Console.WriteLine("\nR - use a random colour " + (prgFiles.Length > 1 ? "for each PRG file" : ""));

                do {
                    key = Console.ReadKey(true);

                    if((key.KeyChar >= '0' && key.KeyChar <= '9') ||
                        (key.Key >= ConsoleKey.A && key.Key <= ConsoleKey.E) ||
                        key.Key == ConsoleKey.R) {
                        // We have a valid selection, so just break out of the infinite loop
                        break;
                    }
                }
                while(true);

                if(key.Key == ConsoleKey.R) {
                    useRandomTextColour = true;

                    // Just add a colour code which will be replaced each time we generate a random colour
                    chrCodePrefix.Add(chrColourMapping[textColourNum]);
                }
                else {
                    if(key.KeyChar <= '9') {
                        textColourNum = key.KeyChar - '0';
                    }
                    else {
                        textColourNum = (key.Key - ConsoleKey.A) + 10;
                    }

                    // If the user didn't select the default light blue text colour, then
                    // append the appropriate character code for their chosen colour
                    if(textColourNum != 0x9a) {
                        chrCodePrefix.Add(chrColourMapping[textColourNum]);
                    }
                }

                // Read in the CBM header containing the Gyrospeed loader
                var cbmHeaderBuf = File.ReadAllBytes(Path.Combine(exePath, GYROSPEED_HEADER_FILENAME));

                // Create a byte array containing a sufficient number of spaces which can be used to blank out both
                // file names and loading effect code in the header buf allowing it to be re-used for multiple files
                var blankingBytes = Encoding.ASCII.GetBytes(string.Empty.PadLeft(cbmHeaderBuf.Length - GYROSPEED_HEADER_LOADING_EFFECT_ROUTINE_OFFSET, ' '));

                // Read in Gyrospeed boot routine file
                // This routine is called after the headers have been loaded as it hijacks the BASIC idle loop vector at $0302.
                // It then calls the loader code stored in the CBM header at offset $0351. It then finally starts
                // the program after the turbo load has finished by performing a BASIC RUN.
                var gyroBootBuf = File.ReadAllBytes(Path.Combine(exePath, GYROSPEED_BOOT_FILENAME));

                Console.Clear();

                foreach(var file in prgFiles) {
                    Console.Write($"Processing {file.Name} - ");

                    // Read in crunched PRG
                    var crunchedPrgBuf = File.ReadAllBytes(file.Path);

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

                    // If the user has selected random effects, then generate one
                    if(useRandomLoadingEffect) {
                        var randomLoadingEffectNum = 0;

                        do {
                            randomLoadingEffectNum = rnd.Next(0, LoadingEffects.Styles.Length);
                        }
                        while(randomLoadingEffectNum == loadingEffectNum);

                        loadingEffectNum = randomLoadingEffectNum;
                    }

                    var loadingEffect = LoadingEffects.Styles[loadingEffectNum];

                    // If the user has selected random text colours, then generate one
                    if(useRandomTextColour) {
                        var randomTextColourNum = 0;

                        do {
                            randomTextColourNum = rnd.Next(0, chrColourMapping.Length);
                        }
                        while(randomTextColourNum == textColourNum);

                        textColourNum = randomTextColourNum;

                        // Update the random colour character in the prefix
                        chrCodePrefix[chrCodePrefix.Count - 1] = chrColourMapping[textColourNum];
                    }

                    // The filename length is determined by the users choices regarding the found screen style, as both clear screen and
                    // change text colour take one character each, leaving only 14 or 15 characters left for the actual filename itself.
                    var fileNameLength = CBM_HEADER_MAX_FILENAME_LENGTH - chrCodePrefix.Count;

                    // Strip off the "-[ex]" suffix if present (i.e. if this is a crunched PRG from the OneLoad64 games collection)
                    file.FileNameWithoutExtension = Regex.Replace(Path.GetFileNameWithoutExtension(file.Name), "-\\[ex\\]", "", RegexOptions.IgnoreCase);

                    // Just cheat and convert to uppercase so that it'll display correctly in PETSCII
                    var fileNameUpper = file.FileNameWithoutExtension.ToUpper();

                    // Trim to available characters if necessary
                    if(fileNameUpper.Length > fileNameLength) {
                        fileNameUpper = fileNameUpper.Substring(0, fileNameLength);
                    }

                    var fileNameBytes = Encoding.UTF8.GetBytes(fileNameUpper);

                    if(chrCodePrefix.Count > 0) {
                        // Add the character prefix which can be used to clear the screen and change the text colour
                        fileNameBytes = chrCodePrefix.Concat(fileNameBytes).ToArray();
                    }

                    // Insert the filename into the CBM header
                    Array.Copy(blankingBytes, 0, cbmHeaderBuf, 7, CBM_HEADER_MAX_FILENAME_LENGTH);
                    Array.Copy(fileNameBytes, 0, cbmHeaderBuf, 7, fileNameBytes.Length);

                    // Add the loading effect
                    Array.Copy(blankingBytes, 0, cbmHeaderBuf, GYROSPEED_HEADER_LOADING_EFFECT_ROUTINE_OFFSET, cbmHeaderBuf.Length - GYROSPEED_HEADER_LOADING_EFFECT_ROUTINE_OFFSET);
                    Array.Copy(loadingEffect, 0, cbmHeaderBuf, GYROSPEED_HEADER_LOADING_EFFECT_ROUTINE_OFFSET, loadingEffect.Length);

                    // Calculate the CBM header checksum
                    // Start at index 2 because first two bytes are the load address ($033c)
                    // Note: bytes 4, 5, 6 and 7 specify the start and end address of the Gyrospeed boot routine (e.g. $02bc and $0304)
                    var cbmHeaderBufCheckSum = CalcXorChecksum(cbmHeaderBuf, 2);

                    // Calculate the Gyrospeed boot routine checksum
                    // Start at index 2 because first two bytes are the load address ($02bc)
                    var gyroBootBufCheckSum = CalcXorChecksum(gyroBootBuf, 2);

                    file.TapPath = Path.Combine(pathToWriteTapFiles, $"{file.FileNameWithoutExtension}.tap");

                    using(var tapStream = File.Open(file.TapPath, FileMode.Create))
                    using(var binWriter = new BinaryWriter(tapStream)) {
                        // Write the TAP file header
                        binWriter.Write(TAP_FILE_MAGIC.ToCharArray()); // 12 bytes
                        binWriter.Write(TAP_FILE_VERSION); // 1 byte

                        // Reserved (3 bytes)
                        binWriter.Write((byte)0x00);
                        binWriter.Write((byte)0x00);
                        binWriter.Write((byte)0x00);

                        // Data size - zero for now - will get updated after the complete TAP file has been written
                        binWriter.Write(0x00); // 4 bytes

                        prgFileLengthInSeconds = 0;

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
                        file.TapDataSize = tapStream.Position - TAP_FILE_HEADER_SIZE;
                        tapStream.Seek(TAP_FILE_DATA_LENGTH_OFFSET, SeekOrigin.Begin);
                        binWriter.Write((int)file.TapDataSize);
                    }

                    file.TapDurationInSeconds = prgFileLengthInSeconds;
                    Console.WriteLine($"  Running length: {TimeSpan.FromSeconds(prgFileLengthInSeconds):mm\\:ss}");
                }

                // Join the created TAP files into compilations if requested by the user
                if(buildCompilationTapFiles) {
                    Console.Write($"\nBuilding compilations...");
                    BuildCompilations(prgFiles, pathToWriteTapFiles, compilationTapeLength, buildIndividualTapFiles);
                }

                return 0;
            }
            catch(Exception ex) {
                Console.WriteLine($"\n{ex.Message}");
                WaitForKeyAndExit();
            }

            return -1;
        }

        static void BuildCompilations(PrgFile[] prgFiles, string pathToWriteTapFiles, int compilationTapeLength, bool keepIndividualTapFiles) {
            // Find the best way to distribute the PRG files across tapes of the provided length using a simple offline bin packing algorithm
            var cassetteSidesRequired = BinPacking.BestFitDecreasing(prgFiles, (compilationTapeLength / 2) * 60);

            var cassetteId = 1;

            for(var i = 0; i < cassetteSidesRequired; i += 2) {
                var cassetteName = $"C64 Compilation Cassette #{cassetteId}";

                // Get the items assigned to the current two sides of a cassette
                var sideAPrgFiles = prgFiles.Where(p => p.BinNumber == i).ToArray();
                var sideBPrgFiles = prgFiles.Where(p => p.BinNumber == i + 1).ToArray();

                // Sort the items on each side alphabetically
                Array.Sort(sideAPrgFiles, (prg1, prg2) => StringComparer.InvariantCultureIgnoreCase.Compare(prg1.FileNameWithoutExtension, prg2.FileNameWithoutExtension));
                Array.Sort(sideBPrgFiles, (prg1, prg2) => StringComparer.InvariantCultureIgnoreCase.Compare(prg1.FileNameWithoutExtension, prg2.FileNameWithoutExtension));

                // Now join the tap files into combined TAP files representing each side of the cassette
                JoinTapFiles(sideAPrgFiles, Path.Combine(pathToWriteTapFiles, $"{cassetteName} - Side A.tap"));
                JoinTapFiles(sideBPrgFiles, Path.Combine(pathToWriteTapFiles, $"{cassetteName} - Side B.tap"));

                // Output a file listing the contents of both sides
                using(var outputFile = new StreamWriter(Path.Combine(pathToWriteTapFiles, $"{cassetteName} - Contents.txt"))) {
                    var longestNumberOfItems = sideAPrgFiles.Count() > sideBPrgFiles.Count() ? sideAPrgFiles.Count() : sideBPrgFiles.Count();
                    var longestSideAItemName = sideAPrgFiles.OrderByDescending(p => p.FileNameWithoutExtension.Length).First().FileNameWithoutExtension.Length;
                    var sideHeadingsPadding = string.Empty.PadRight(longestSideAItemName + 4 - 6);

                    outputFile.WriteLine();
                    outputFile.WriteLine(cassetteName);
                    outputFile.WriteLine();
                    outputFile.WriteLine($"Side A{sideHeadingsPadding}Side B");
                    outputFile.WriteLine($"------{sideHeadingsPadding}------");
                    outputFile.WriteLine();

                    for(var j = 0; j < longestNumberOfItems; j++) {
                        if(sideAPrgFiles.Count() >= j + 1) {
                            outputFile.Write($"{sideAPrgFiles.ElementAt(j).FileNameWithoutExtension}".PadRight(longestSideAItemName + 4));
                        }

                        if(sideBPrgFiles.Count() >= j + 1) {
                            // Add padding if there isn't an item at this index on side A
                            if(sideAPrgFiles.Count() < j + 1) {
                                outputFile.Write(string.Empty.PadLeft(longestSideAItemName + 4));
                            }

                            outputFile.Write($"{sideBPrgFiles.ElementAt(j).FileNameWithoutExtension}");
                        }

                        outputFile.WriteLine();
                    }

                    outputFile.WriteLine();
                    outputFile.WriteLine($"Length{sideHeadingsPadding}Length");
                    outputFile.WriteLine($"------{sideHeadingsPadding}------");
                    outputFile.WriteLine();
                    outputFile.WriteLine($"{TimeSpan.FromSeconds(sideAPrgFiles.Sum(p => p.TapDurationInSeconds)):mm\\:ss} {sideHeadingsPadding}{TimeSpan.FromSeconds(sideBPrgFiles.Sum(p => p.TapDurationInSeconds)):mm\\:ss}");
                }

                cassetteId++;
            }

            // If the user didn't want individual TAP files too, then delete them now that we've finished building the compilations
            if(!keepIndividualTapFiles) {
                foreach(var file in prgFiles) {
                    File.Delete(file.TapPath);
                }
            }
        }

        static void JoinTapFiles(PrgFile[] prgFiles, string tapFileName) {
            // If there are no PRG files in the list, then just return
            if(prgFiles.Length == 0) {
                return;
            }

            using(var tapStream = File.Open(tapFileName, FileMode.Create))
            using(var binWriter = new BinaryWriter(tapStream)) {
                // Write the TAP file header
                binWriter.Write(TAP_FILE_MAGIC.ToCharArray()); // 12 bytes
                binWriter.Write(TAP_FILE_VERSION); // 1 byte

                // Reserved (3 bytes)
                binWriter.Write((byte)0x00);
                binWriter.Write((byte)0x00);
                binWriter.Write((byte)0x00);

                // Data size (sum of all the TAP files data sizes)
                binWriter.Write((int)prgFiles.Sum(p => p.TapDataSize)); // 4 bytes

                foreach(var file in prgFiles) {
                    // Read in the TAP file
                    var tapFileBuf = File.ReadAllBytes(file.TapPath);

                    // Write the TAP file data to the joined TAP file (skipping the TAP header)
                    binWriter.Write(tapFileBuf, TAP_FILE_HEADER_SIZE, tapFileBuf.Length - TAP_FILE_HEADER_SIZE);
                }
            }
        }

        static bool AskYesOrNo(string question) {
            ConsoleKeyInfo key;

            ClearConsoleAndWriteLine($"\n{question}");

            do {
                key = Console.ReadKey(true);
            }
            while(key.Key != ConsoleKey.Y && key.Key != ConsoleKey.N);

            return key.Key == ConsoleKey.Y;
        }

        static void WaitForKeyAndExit() {
            Console.ReadKey(true);
            Environment.Exit(-1);
        }

        static void ClearConsoleAndWriteLine(string text) {
            Console.Clear();
            Console.WriteLine(text);
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
                        WriteGyrospeedOnBit(binWriter);
                    }
                    // bit = 0
                    else {
                        WriteGyrospeedOffBit(binWriter);
                    }
                }

                // Keep a track of all the data bytes XOR'd together
                gyroSpeedCheckSum ^= data;
            }
            // Write standard CBM pulses
            else {
                WriteNewDataMarker(binWriter);

                bool checkbit = true;
                int bits;

                for(bits = 0; bits <= 7; bits++) {
                    // bit = 1
                    if((data & (1 << bits)) != 0) {
                        checkbit = !checkbit;

                        WriteOnBit(binWriter);
                    }
                    // bit = 0
                    else {
                        WriteOffBit(binWriter);
                    }
                }

                // Write the checkbit (parity)
                if(checkbit) {
                    WriteOnBit(binWriter);
                }
                else {
                    WriteOffBit(binWriter);
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
            prgFileLengthInSeconds += CalculatePulseLengthInSeconds(SHORT_PULSE) * length;

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

            prgFileLengthInSeconds += CalculateClockCyclesInSeconds(clockCycles);
        }

        static void WriteNewDataMarker(BinaryWriter binWriter) {
            binWriter.Write(LONG_PULSE);
            binWriter.Write(MEDIUM_PULSE);

            prgFileLengthInSeconds += CalculatePulseLengthInSeconds(LONG_PULSE) + CalculatePulseLengthInSeconds(MEDIUM_PULSE);
        }

        static void WriteEndOfDataMarker(BinaryWriter binWriter) {
            binWriter.Write(LONG_PULSE);
            binWriter.Write(SHORT_PULSE);

            prgFileLengthInSeconds += CalculatePulseLengthInSeconds(LONG_PULSE) + CalculatePulseLengthInSeconds(SHORT_PULSE);
        }

        static void WriteOnBit(BinaryWriter binWriter) {
            binWriter.Write(MEDIUM_PULSE);
            binWriter.Write(SHORT_PULSE);

            prgFileLengthInSeconds += CalculatePulseLengthInSeconds(MEDIUM_PULSE) + CalculatePulseLengthInSeconds(SHORT_PULSE);
        }

        static void WriteOffBit(BinaryWriter binWriter) {
            binWriter.Write(SHORT_PULSE);
            binWriter.Write(MEDIUM_PULSE);

            prgFileLengthInSeconds += CalculatePulseLengthInSeconds(SHORT_PULSE) + CalculatePulseLengthInSeconds(MEDIUM_PULSE);
        }

        static void WriteGyrospeedOnBit(BinaryWriter binWriter) {
            binWriter.Write(GYROSPEED_ON_BIT);

            prgFileLengthInSeconds += CalculatePulseLengthInSeconds(GYROSPEED_ON_BIT);
        }

        static void WriteGyrospeedOffBit(BinaryWriter binWriter) {
            binWriter.Write(GYROSPEED_OFF_BIT);

            prgFileLengthInSeconds += CalculatePulseLengthInSeconds(GYROSPEED_OFF_BIT);
        }

        static double CalculatePulseLengthInSeconds(byte data) {
            return (data * 8) / PAL_CPU_FREQUENCY_IN_HZ;
        }

        static double CalculateClockCyclesInSeconds(int clockCycles) {
            return clockCycles / PAL_CPU_FREQUENCY_IN_HZ;
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
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
