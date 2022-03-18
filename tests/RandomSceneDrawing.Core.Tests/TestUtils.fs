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
    let waitWith timeout predicate =
        Async.StartChild(
            async {
                while predicate () do
                    do! Async.Sleep 10
            },
            timeout
        )

    let elmishCmdMsgAsync testMessage expectMsgs (actualCmd: Cmd<'msg>) =
        async {
            let actualMsgs = System.Collections.Generic.List()
            List.iteri (fun i sub -> sub (fun msg -> actualMsgs.Add msg)) actualCmd
            let! w = waitWith 5000 (fun _ -> actualMsgs.Count <> actualCmd.Length)
            do! w

            Expect.sequenceEqual actualMsgs expectMsgs $"{testMessage}"
        }


    let elmishUpdate (update: Update<'msg, 'model>) testMessage initModel msg msgMapper expectModel expectMsgs =

        let actualModel, actualCmd =
            update
            |> Elmish.foldMessages initModel  msg msgMapper

        Expect.equal actualModel expectModel testMessage

        elmishCmdMsgAsync "Msg" expectMsgs actualCmd


    let model update testMessage initModel msg msgMapper expectedUpdatedModel =

        let actual, _ =
            update
            |> Elmish.foldMessages initModel [ msg ] msgMapper

        Expect.equal actual expectedUpdatedModel testMessage
