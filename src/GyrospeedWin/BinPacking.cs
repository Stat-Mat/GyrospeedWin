using System;

namespace GyrospeedWin {
    public class BinPacking {
        // Assigns PRG files files to an appropriate bin given it's duration in seconds and
        // returns the total number of bins required using the offline best fit decreasing algorithm 
        public static int BestFitDecreasing(PrgFile[] prgFiles, int binSizeInSeconds) {
            // First sort into decreasing order 
            Array.Sort(prgFiles, (prg1, prg2) => prg1.TapDurationInSeconds.CompareTo(prg2.TapDurationInSeconds));
            Array.Reverse(prgFiles);

            var numBinsRequired = 0;

            // Create an array to store remaining space in bins 
            // there can be at most n bins 
            var bin_rem = new double[prgFiles.Length];

            // Place items one by one 
            for(var i = 0; i < prgFiles.Length; i++) {
                // Initialize minimum space left and index of best bin 
                double min = binSizeInSeconds + 1;
                int bestBin = 0;

                // Find the best bin that can accomodate prgFiles[i] 
                for(var j = 0; j < numBinsRequired; j++) {
                    if(bin_rem[j] >= prgFiles[i].TapDurationInSeconds &&
                        bin_rem[j] - prgFiles[i].TapDurationInSeconds < min) {
                        bestBin = j;
                        min = bin_rem[j] - prgFiles[i].TapDurationInSeconds;
                    }
                }

                // If no bin could accommodate prgFiles[i], create a new bin 
                if(min == binSizeInSeconds + 1) {
                    bin_rem[numBinsRequired] = binSizeInSeconds - prgFiles[i].TapDurationInSeconds;
                    prgFiles[i].BinNumber = numBinsRequired;
                    numBinsRequired++;
                }
                else {
                    // Assign the PRG file to the best bin
                    bin_rem[bestBin] -= prgFiles[i].TapDurationInSeconds;
                    prgFiles[i].BinNumber = bestBin;
                }
            }

            return numBinsRequired;
        }
    }
}