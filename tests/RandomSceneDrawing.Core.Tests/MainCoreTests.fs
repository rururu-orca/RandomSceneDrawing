module RandomSceneDrawing.Tests.Main


open Expecto
open RandomSceneDrawing.Main

let api =
    { step = fun _ -> async { do! Async.Sleep 2 }}
let init = init () ()
let updatePlayer = update api DrawingSettings.api

[<Tests>]
let mainPlayerTest =
    Player.Core.msgTestSet
        "Model.MainPlayer"
        init
        (fun model state -> { model with MainPlayer = state })
        (fun msg -> PlayerMsg(MainPlayer, msg))
        updatePlayer

[<Tests>]
let subPlayerTest =
    Player.Core.msgTestSet
        "Model.SubPlayer"
        init
        (fun model state -> { model with SubPlayer = state })
        (fun msg -> PlayerMsg(SubPlayer, msg))
        updatePlayer

let updateSettings settingsApi =
    update api settingsApi Player.Core.apiOk

[<Tests>]
let settingTest =
    DrawingSettings.msgTestSet
        "Model.Settings"
        init
        (fun model state -> { model with Settings = state })
        SettingsMsg
        updateSettings

