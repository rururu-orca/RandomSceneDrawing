module RandomSceneDrawing.PlayerLib.FFmpeg

open System.Diagnostics
open Cysharp.Diagnostics
open FsToolkit.ErrorHandling
open RandomSceneDrawing.Util
open FSharp.Control

module ProcessAsyncEnumerable =
    open System.Collections.Generic

    let addListAsync (list: List<_>) (output: ProcessAsyncEnumerable) =
        AsyncSeq.ofAsyncEnum output
        |> AsyncSeq.iter list.Add


let inline runAsync args =
    task {
        let args' = String.concat " " args
        let startInfo = ProcessStartInfo(config.PlayerLib.FFmpegPath, Arguments = args')
        let errs = System.Collections.Generic.List()

        try
            let struct (_, out, err) = ProcessX.GetDualAsyncEnumerable(startInfo)

            do! ProcessAsyncEnumerable.addListAsync errs err
            do! out.WriteLineAllAsync()

            return Ok()
        with
        | :? ProcessErrorException as ex ->
            let msg = $"ExitCode{ex.ExitCode}: %A{List.ofSeq errs}"
            return Error msg
    }

let inline trimMediaAsync startTime endTime inputPath destinationPath =
    [ $"-loglevel warning -y -ss %.3f{startTime} -to %.3f{endTime} -copyts -i \"{inputPath}\" -c:v copy -an \"{destinationPath}\"" ]
    |> runAsync

let inline resizeMediaAsync inputPath rescaleParam destinationPath =
    let nvenc =
        [ "-loglevel warning -y -hwaccel cuda -hwaccel_output_format cuda -init_hw_device vulkan=vk:0 -filter_hw_device vk"
          $"-copyts -i \"{inputPath}\""
          $"-vf \"hwupload,libplacebo={rescaleParam}:p010le,hwupload_cuda\""
          $"-c:v hevc_nvenc -an \"{destinationPath}\"" ]

    let qsv =
        [ "-loglevel warning -y"
          $"-copyts -i \"{inputPath}\""
          $"-vf \"scale={rescaleParam}\""
          $"-c:v hevc_qsv -an \"{destinationPath}\"" ]

    let any =
        [ "-loglevel warning -y"
          $"-copyts -i \"{inputPath}\""
          $"-vf \"scale={rescaleParam}\""
          $"-c:v hevc -an \"{destinationPath}\"" ]

    runAsync nvenc
    |> TaskResult.orElse (runAsync qsv)
    |> TaskResult.orElse (runAsync any)
