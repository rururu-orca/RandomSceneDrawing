module RandomSceneDrawing.PlayerLib.Utils

open FSharp.Control
open System.Threading
open System.Threading.Tasks

let inline toSecf mSec = float mSec / 1000.0

let internalAsync sub unsub action (tcs: TaskCompletionSource) (ctr: CancellationTokenRegistration) =
    task {
        try
            sub ()
            action ()
            return! tcs.Task.ConfigureAwait false
        finally
            unsub ()
            ctr.Dispose()
    }
