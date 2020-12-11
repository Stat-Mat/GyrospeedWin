# GyrospeedWin

GyrospeedWin is a C#/.NET Windows replacement for the native Commodore 64 Gyrospeed turbo tape program written by Gary Saunders. It simplifies the creation of turbo tape (.TAP) images from crunched PRG files, which can be recorded back to cassette tape for use on real hardware. The Gyrospeed loader is one of the quickest turbos around, with many crunched games loading in around 20 - 30 counts of the datasette counter.

The original Gyrospeed program was included as a type-in listing in the April 1988 issue of Your Commodore magazine. The repository contains a .D64 image containing the original program which can be downloaded [here](https://github.com/Stat-Mat/GyrospeedWin/blob/master/Gyrospeed%20%28TOOL%29%20%28G.%20SAUNDERS%29.d64). The magazine issue can be viewed on the Internet Archive here:

[![your-commodore-apr88-cover](https://github.com/Stat-Mat/GyrospeedWin/blob/master/your-commodore-apr88-cover.jpg)](https://archive.org/details/YourCommodore80Jun91/YourCommodore/YourCommodore43-Apr88/page/n67/mode/2up)

![gyrospeed-title-screen](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeed-title-screen.jpg)

## Usage

GyrospeedWin \<crunched-prg-file> -or- \<folder-containing-crunched-prgs>

Alternatively, you can simply drag and drop a PRG file or folder containing PRG files onto the executable filename in Windows Explorer.

The crunched PRG files should have a BASIC SYS line at $0801 to run the program (e.g. crunched with Exomizer etc) and can be loaded into the address range $0400 - $cfff.

## Features

* Fixes the issue with the original Gyrospeed where it did not clear the keyboard buffer before starting the crunched program
* Supports processing either a single PRG file or batch processing all PRGs in a given folder (drag and drop)
* Supports 10 different loading effects such as the classsic FreeLoad colour cycling from the Ocean Loader, as well an option for randomised effect selection
* Includes full C# and 6502 assembly sourcecode

## Screenshots

![gyrospeedwin-help](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeedwin-help.jpg)

![gyrospeedwin-processing](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeedwin-processing.jpg)

![gyrospeedwin-loading-effect-0](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeedwin-loading-effect-0.jpg) ![gyrospeedwin-loading-effect-2](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeedwin-loading-effect-2.jpg)

![gyrospeedwin-loading-effect-6](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeedwin-loading-effect-6.jpg) ![gyrospeedwin-loading-effect-7](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeedwin-loading-effect-7.jpg)

![gyrospeedwin-loading-effect-8](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeedwin-loading-effect-8.jpg) ![gyrospeedwin-loading-effect-9](https://github.com/Stat-Mat/GyrospeedWin/blob/master/gyrospeedwin-loading-effect-9.jpg)

## Thanks

Special thanks go out to the following people for their contributions and support:

* ricky006 for introducing me to the original Gyrospeed program and providing invaluable testing on real hardware 
* SLC (the author of TapEx) for his fantastic help with some questions I had about the .TAP format
* Richard of TND for providing additional loading effects, including a couple of amusing ones! ;)
