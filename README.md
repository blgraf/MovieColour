# MovieColour
A program to create movie barcodes from your favourite movies and use them as wallpapers.

## Description
With the help of [FFmpeg](https://ffmpeg.org/) to extract frames, the program analyses each frame of the desired video file and determines the most relevant colour of that frame based on different methods, e.g. the most frequent colour. And then creates a wallpaper-sized image from it.

## Installation
Clone the repo and execute the `publish.bat` file. This will create a self-contained .exe file in `release/`.  
One day there will be a release page.
### Requirements
- .NET 6
- Windows 10+
- FFmpeg & FFprobe to be run from the command prompt
- Something like at least 5GB of RAM (everything except conversion is done in memory)
  
The plan is to update it to .NET 8 soonish.

## Usage
Soon(TM) there will be a proper instruction with screenshots.  
- Run the file and select a video file.
- Select the analysis methods, and whether or not the video should be converted to a smaller scale*
- Select if the source file should be converted to a smaller scale first and if so set the size (format: `desiredResolution:-2`, e.g. `480:-2`)
- Start the process. A converted movie file and the resulting pictures will be stored in the same directory as the movie file

*anything below 1080p drastically increases the processing time.  
From my testing, on a Ryzen 9 3900X, analysing one batch of frames takes between 20 and 40 seconds on average.  
And on 4k one batch is about 80 frames, and on 1080p 360 frames.  
This means, that a 2h movie on 24fps takes about four hours on 1080p, and 18 hours on 4k. (Very rough estimation)

## Known Issues
Yes.  
There are [many things](https://github.com/blgraf/MovieColour/issues) (still in progress).  
Short summary:
- Error handling and log messages
- Progress bars are wonky due to a breaking change in an earlier version)
- Cancellation
- UI is "working"
- Conversion resolution in the UI needs improvement
- Xabe.FFmpeg is still used in some places from an earlier version and needs to be removed
- No detection if FFmpeg and FFprobe are installed (which are a requirement)
- Only two properly implemented analysis methods

## Contribution
Idk, fork, pull request?

## Big future goals
- Use the GPU to process the frames
- Detection of the background and weigh that less
- Add a benchmark for your system

## Licensing
The program MovieColour and its source code are published under GPL v3.

## Acknowledgement
This is inspired heavily by posts like this from Tumblr: [Harry Potter series by moviebarcode](https://moviebarcode.tumblr.com/post/12390371286/harry-potter-complete-series-2001-2011-prints)  
I am not sure this is the first post but among the oldest I could find. Upon closer inspection, I realised that the main method they used appeared to be a compression of the frame onto a one-pixel wide image, and stitched those together.  
While this might work in some cases, it doesn't allow for too much customisation, so I started this project.
