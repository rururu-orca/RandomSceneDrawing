#r "nuget: FSharpPlus"
#r "nuget: ProcessX"
#r "nuget: FSharp.Control.AsyncSeq"

open FSharpPlus
open FSharp.Control
open Cysharp.Diagnostics
open System
open System.IO


task {
    let USERPROFILE = Environment.GetEnvironmentVariable "USERPROFILE"
    let input = $"{USERPROFILE}/temp/imascgstage.exe/output22.mp4"
    let output = "output.mp4"
    let width = 300
    let startTime = 20000
    let dur = 5000
    let args = 
        [
            "ffmpeg"
            "-hwaccel cuda -hwaccel_output_format cuda -init_hw_device vulkan=vk:0 -filter_hw_device vk"
            $"-ss {startTime}ms -i {input} -t {dur}ms"
            $"-vf \"hwupload,libplacebo={width}:-1:p010le,hwupload_cuda\""
            $" -c:v hevc_nvenc -c:a copy {output}"
        ]
        |> String.concat " "
    do! ProcessX.StartAsync(args).WriteLineAllAsync()
}
|> Async.AwaitTask
|> Async.RunSynchronously

ProcessX.StartAsync
    "powershell -nop -NonInteractive -c Get-PnpDevice -InstanceId *1C14A083-0001-1010-8000-0025DCDF3531 | Get-PnpDeviceProperty DEVPKEY_Device_LocationInfo | % Data"
|> AsyncSeq.ofAsyncEnum
|> AsyncSeq.iter (printfn "%A")
|> Async.RunSynchronously

"dlna-playsingle://uuid:1C14A083-0001-1010-8000-0025DCDF3531?sid=urn:upnp-org:serviceId:ContentDirectory&iid=0/video/folder/fol1/fol5/fol10/con24"
|> String.split [ "uuid:" ]
|> Seq.last
|> String.split [ "?"; "&" ]

let replace p r input =
    System.Text.RegularExpressions.Regex.Replace(input, p, replacement = r)

System.DateTime.Now.ToString("yyyyMMdd")
|> printfn "%s"

let sprintfDateTime format (datetime: DateTime) = datetime.ToString(format = format)

let sprintfNow format = DateTime.Now |> sprintfDateTime format

sprintfNow "yyyyMMdd"

Directory.Exists @"C:\Users\localadmin\OneDrive\Apps\portableApp\PowershellModule\RandomDrawing\screenshot\2021052\000"

"iid=0/video/folder/fol1/fol5/fol10/con24"
|> String.split [ "/" ]
|> Seq.last
// |> split "uuid:"
// |> Seq.last
// |> split "[\?&]"
// |> matchString "(?=uuid:)[^?]+"

let getCurrentValueAndNextState currentState = Some(currentState, currentState + 1)
printfn "%03i" 1

open Microsoft.FSharp.Reflection

type TestCase =
    | Text1
    | Text3
    static member GetSeqValues() =
        // Get all cases of the union
        FSharpType.GetUnionCases(typeof<TestCase>)
        |> Seq.map (fun c -> FSharpValue.MakeUnion(c, [||]) :?> TestCase)

    static member GetSeqLabels() =
        FSharpType.GetUnionCases(typeof<TestCase>)
        |> Seq.map (fun c -> c.Name)

type TestRecord =
    { Text1: string; Text2: string }
    static member GetSeqLabels() =
        FSharpType.GetRecordFields(typeof<TestRecord>)
        |> Seq.map (fun c -> c.Name)



TestCase.GetSeqLabels()
TestRecord.GetSeqLabels()

let mapCase =
    function
    | Text1 -> 1
    | Text3 -> 3
