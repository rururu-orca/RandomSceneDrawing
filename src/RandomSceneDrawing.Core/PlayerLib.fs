module RandomSceneDrawing.PlayerLib

open System
open LibVLCSharp.Shared

let getMediaFromlocal (source: string) libvlc =
    new Media(libvlc, (Uri source))