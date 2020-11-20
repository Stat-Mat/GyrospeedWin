# GyrospeedWin

GyrospeedWin is a C#/.NET Windows replacement for the native Commodore 64 Gyrospeed application written by Gary Saunders. It simplifies the creation of turbo tapes (.TAP) images from crunched PRG files, which can either be used in emulators, or recorded back to cassette tape for use on real hardware. The original Gyrospeed application was included as a type-in listing in the April 1988 issue of Your Commodore magazine. The repository contains a .D64 image containing a PRG of the original application which can be downloaded [here](https://github.com/Stat-Mat/GyrospeedWin/blob/master/Gyrospeed%20%28TOOL%29%20%28G.%20SAUNDERS%29.d64). The magazine issue can be viewed on the Internet Archive here:

[![your-commodore-apr88-cover](https://github.com/Stat-Mat/GyrospeedWin/blob/master/your-commodore-apr88-cover.jpg)](https://archive.org/details/YourCommodore80Jun91/YourCommodore/YourCommodore43-Apr88/page/n67/mode/2up)

![gyrospeed-title-screen](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeed-title-screen.jpg)

## Usage

GyrospeedWin <crunched-prg-file> -or- <folder-containing-crunched-prgs>

The crunched PRG files should have a BASIC SYS line at $0801 to run the program (e.g. crunched with Exomizer etc) and can be loaded into the address range $0400 - $cfff.

## Features

* Fixes the issue with the original Gyrospeed where it did not clear the keyboard buffer before starting the crunched program
* Supports processing either a single PRG file or batch processing all PRGs in a given folder (drag and drop)
* Supports 10 different loading effects, including an option for randomised selection
* Includes full C# and 6502 assembler sourcecode

## Screenshots

![gyrospeedwin-help](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeedwin-help.jpg)

![gyrospeedwin-processing](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeedwin-processing.jpg)

![gyrospeedwin-loading-effect-0](https://github.com/GyrospeedWin/gyrospeedwin/blob/master/gyrospeedwin-loading-effect-0.jpg) ![gyrospeedwin-loading-effect-2](https://github.com/GyrospeedWin/gyrospeedwin/blob/master/gyrospeedwin-loading-effect-2.jpg)

![gyrospeedwin-loading-effect-6](https://github.com/GyrospeedWin/gyrospeedwin/blob/master/gyrospeedwin-loading-effect-6.jpg) ![gyrospeedwin-loading-effect-7](https://github.com/GyrospeedWin/gyrospeedwin/blob/master/gyrospeedwin-loading-effect-7.jpg)

![gyrospeedwin-loading-effect-8](https://github.com/GyrospeedWin/gyrospeedwin/blob/master/gyrospeedwin-loading-effect-8.jpg) ![gyrospeedwin-loading-effect-9](https://github.com/GyrospeedWin/gyrospeedwin/blob/master/gyrospeedwin-loading-effect-9.jpg)

## Thanks

Special thanks go out to the following people for their contributions and support:

* ricky006 for introducing me to the original Gyrospeed program and providing invaluable testing on real hardware 
* SLC (author of TapEx) for his fantastic help with some questions I had about the .TAP format
