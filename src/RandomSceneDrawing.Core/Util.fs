module RandomSceneDrawing.Util

open FSharp.Configuration
open System
open System.IO

let inline tap ([<InlineIfLambda>] sideEffect) n =
    sideEffect n
    n

type Config = YamlConfig<"Config.yaml">

let changedConfigPath =
    Path.Combine [|
        AppDomain.CurrentDomain.BaseDirectory
        "ChangedConfig.yaml"
    |]
let config = Config()

if File.Exists changedConfigPath then
    config.Load changedConfigPath