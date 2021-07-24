module RandomSceneDrawing.PlayerLib

open System
open LibVLCSharp.Shared

Core.Initialize()
let libvlc = new LibVLC("--verbose=2")
let mediaPlayer = new MediaPlayer(libvlc)

let getMediaFromlocal (source: string) libvlc =
   new Media(libvlc, (Uri source))