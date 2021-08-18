#r "nuget: FSharpPlus"
#r "nuget: ProcessX, 1.3.0"
#r "nuget: FSharp.Control.AsyncSeq, 3.0.5"

open FSharpPlus
open FSharp.Control
open Cysharp.Diagnostics
open System
open System.IO

ProcessX.StartAsync "powershell -nop -NonInteractive -c Get-PnpDevice -InstanceId *1C14A083-0001-1010-8000-0025DCDF3531 | Get-PnpDeviceProperty DEVPKEY_Device_LocationInfo | % Data"
|> AsyncSeq.ofAsyncEnum
|> AsyncSeq.iter (printfn "%A")
|> Async.RunSynchronously

"dlna-playsingle://uuid:1C14A083-0001-1010-8000-0025DCDF3531?sid=urn:upnp-org:serviceId:ContentDirectory&iid=0/video/folder/fol1/fol5/fol10/con24"
|> String.split ["uuid:"]
|> Seq.last
|> String.split ["?";"&"]

let replace p r input =
    System.Text.RegularExpressions.Regex.Replace(input , p ,replacement=r)

System.DateTime.Now.ToString("yyyyMMdd")
|> printfn "%s" 

let sprintfDateTime format (datetime:DateTime) =
    datetime.ToString(format=format)

let sprintfNow format =
    DateTime.Now |> sprintfDateTime format

sprintfNow "yyyyMMdd"

Directory.Exists @"C:\Users\localadmin\OneDrive\Apps\portableApp\PowershellModule\RandomDrawing\screenshot\2021052\000"

"iid=0/video/folder/fol1/fol5/fol10/con24"
|> String.split ["/"]
|> Seq.last
// |> split "uuid:" 
// |> Seq.last
// |> split "[\?&]"
// |> matchString "(?=uuid:)[^?]+" 

let getCurrentValueAndNextState currentState = Some(currentState, currentState + 1)
printfn "%03i" 1