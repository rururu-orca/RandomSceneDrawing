module RandomSceneDrawing.PlayerLib

open System
open LibVLCSharp.Shared

let getMediaFromUri source libvlc = new Media(libvlc, (Uri source))
