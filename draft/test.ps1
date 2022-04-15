
$in = "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"
$out = "out.mp4"

# ffmpeg -loglevel warning -y -ss 10 -to 20 -copyts -i $in -c:v copy -an $out
ffprobe.exe -i $in -loglevel warning -show_format