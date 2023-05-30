module RandomSceneDrawing.PlayerLib.FFmpeg

open System.Diagnostics
open System.Threading.Tasks
open Cysharp.Diagnostics
open FsToolkit.ErrorHandling
open RandomSceneDrawing.Util
open FSharp.Control

module ProcessAsyncEnumerable =
    open System.Collections.Generic

    let addListAsync (list: List<_>) (output: ProcessAsyncEnumerable) =
        AsyncSeq.ofAsyncEnum output |> AsyncSeq.iter list.Add


let inline runAsync args = backgroundTask {
    let args' = String.concat " " args
    let startInfo = ProcessStartInfo(config.PlayerLib.FFmpegPath, Arguments = args')
    let errs = System.Collections.Generic.List()

    try
        let struct (_, out, err) = ProcessX.GetDualAsyncEnumerable(startInfo)

        do! ProcessAsyncEnumerable.addListAsync errs err
        do! out.WriteLineAllAsync()

        return Ok()
    with :? ProcessErrorException as ex ->
        if ex.ExitCode = 0 then
            return Ok()
        else
            let msg = $"ExitCode{ex.ExitCode}: %A{List.ofSeq errs}"
#if DEBUG
            printfn $"{msg}"
#endif
            return Error msg
}

let inline orElse ([<InlineIfLambda>] onError: 'u -> Task<Result<'t, 'error>>) args (result: Task<Result<'t, 'error>>) = task {
    match! result with
    | Ok x -> return Ok x
    | Error _ -> return! onError args
}

let inline encodeAsync startTime endTime rescaleParam inputPath destinationPath subDestinationPath =
    let createCommand hwInit mainStreamCommands subStreamCommands = [
        "-loglevel warning -y"
        yield! hwInit
        $"-ss %.3f{startTime} -to %.3f{endTime} -i \"{inputPath}\""
        yield! mainStreamCommands
        $"-an \"{destinationPath}\""
        yield! subStreamCommands
        $"-an \"{subDestinationPath}\""

    ]

    let nvenc =
        createCommand
            [
                "-hwaccel cuda"
                "-hwaccel_output_format cuda"
                "-init_hw_device vulkan=vk:0"
                "-filter_hw_device vk"
                "-extra_hw_frames 2"
            ] [
                "-vf \"hwupload,libplacebo=iw:ih:p010le,hwupload_cuda\""
                "-rc constqp"
                "-qmin 1"
                "-qmax 12"
                "-2pass true"
                "-preset p6"
                "-tune hq"
                "-multipass 2"
                "-c:v hevc_nvenc"
            ] [
                $"-vf \"hwupload,libplacebo={rescaleParam}:p010le,hwupload_cuda\""
                "-c:v hevc_nvenc"
            ]

    let qsv = 
        createCommand
            [] [
                "-vf \"scale=iw:ih\""
                "-c:v hevc_qsv -global_quality 20"
            ] [
                $"-vf \"scale={rescaleParam}\""
                "-c:v hevc_qsv"
            ]


    let any =
        createCommand
            [] [
                "-vf \"scale=iw:ih\""
                "-c:v hevc -global_quality 20"
            ] [
                $"-vf \"scale={rescaleParam}\""
                "-c:v hevc"
            ]

    runAsync nvenc |> orElse runAsync qsv |> orElse runAsync any
