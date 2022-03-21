module RandomSceneDrawing.Tests.Utils

open Expecto
open Elmish

type Update<'msg, 'model> = 'msg -> 'model -> 'model * Cmd<'msg>

module Elmish =
    let foldMessages initialState msgs msgMapper (update: Update<'msg, 'model>) : 'model * Cmd<'msg> =
        msgs
        |> List.map msgMapper
        |> List.fold
            (fun (state, cmd) message ->
                let state', cmd' = update message state
                state', cmd @ cmd')
            (initialState, Cmd.none)

module Expect =

    let elmishCmdMsgAsync testMessage expectMsgs (actualCmd: Cmd<'msg>) =
        async {
            let actualMsgs = System.Collections.Generic.List()

            for sub in actualCmd do
                sub (fun msg ->
                    let count = actualMsgs.Count
                    actualMsgs.Add msg

                    while actualMsgs.Count <> count do
                        let wait = System.Threading.Tasks.Task.Delay 1
                        wait.Wait())


            while actualCmd.Length <> actualMsgs.Count do
                do! Async.Sleep 1

            Expect.sequenceEqual (actualMsgs) expectMsgs $"{testMessage}"
        }


    let elmishUpdate (update: Update<'msg, 'model>) testMessage initModel msg msgMapper expectModel expectMsgs =
        async {
            let actualModel, actualCmd =
                update
                |> Elmish.foldMessages initModel msg msgMapper

            Expect.equal actualModel expectModel testMessage

            do! elmishCmdMsgAsync "Msg" expectMsgs actualCmd
        }


    let model update testMessage initModel msg msgMapper expectedUpdatedModel =

        let actual, _ =
            update
            |> Elmish.foldMessages initModel [ msg ] msgMapper

        Expect.equal actual expectedUpdatedModel testMessage
