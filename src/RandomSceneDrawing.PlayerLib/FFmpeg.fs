module RandomSceneDrawing.PlayerLib.FFmpeg

open System.Diagnostics
open Cysharp.Diagnostics
open FsToolkit.ErrorHandling
open RandomSceneDrawing.Util
open FSharp.Control
open System.Threading.Tasks

let inline runAsync args =
    task {
        let args' = String.concat " " args
        let startInfo = ProcessStartInfo(config.PlayerLib.FFmpegPath, Arguments = args')

        try
            let struct (_, stdout, stderr) = ProcessX.GetDualAsyncEnumerable(startInfo)

            do!
                [| stdout.WriteLineAllAsync()
                   stderr.WriteLineAllAsync() |]
                |> Task.WhenAll

            return Ok()
        with
        | :? ProcessErrorException as ex ->
            let msg = $"%A{ex.ErrorOutput}\n\n{stderr}\n"
            return Error msg
    }

let inline trimMediaAsync startTime endTime inputPath destinationPath =
    [ $"-loglevel warning -y -ss %.3f{startTime} -to %.3f{endTime} -copyts -i \"{inputPath}\" -c:v copy -an \"{destinationPath}\"" ]
    |> runAsync

let inline resizeMediaAsync inputPath rescaleParam destinationPath =
    [ "-loglevel warning -y -hwaccel cuda -hwaccel_output_format cuda -init_hw_device vulkan=vk:0 -filter_hw_device vk"
      $"-copyts -i \"{inputPath}\""
      $"-vf \"hwupload,libplacebo={rescaleParam}:p010le,hwupload_cuda\""
      $"-c:v hevc_nvenc -an \"{destinationPath}\"" ]
    |> runAsync
