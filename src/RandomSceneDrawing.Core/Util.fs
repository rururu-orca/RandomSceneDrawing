module RandomSceneDrawing.Util

open FSharp.Configuration
open System
open System.IO

let inline tap ([<InlineIfLambda>] sideEffect) n =
    sideEffect n
    n

module Task =
    open System.Threading.Tasks
    let millisecondsDelay time =
        Task.Delay(millisecondsDelay = time)

type Config = YamlConfig<"Config.yaml">

let changedConfigPath =
    Path.Combine [|
        AppDomain.CurrentDomain.BaseDirectory
        "ChangedConfig.yaml"
    |]
let config = Config()

if File.Exists changedConfigPath then
    config.Load changedConfigPath

module MailboxProcessor =
    [<RequireQualifiedAccess>]
    type MailboxProcessorMsg<'state, 'u, 'r> =
        | Post of ('state -> 'state)
        | Reply of ('u -> 'r) * AsyncReplyChannel<'r>

    let inline createAgent initialState =
        MailboxProcessor.Start (fun inbox ->
            let rec loop oldState =
                async {
                    match! inbox.Receive() with
                    | MailboxProcessorMsg.Post postFunc ->
                        let newState = postFunc oldState
                        return! loop newState
                    | MailboxProcessorMsg.Reply (replyFunc, ch) ->
                        replyFunc oldState |> ch.Reply
                        return! loop oldState
                }

            loop initialState)

    let inline post (agent: MailboxProcessor<MailboxProcessorMsg<'p, 'u, 'r>>) postFunc =
        MailboxProcessorMsg.Post postFunc |> agent.Post

    let inline postAndReply (agent: MailboxProcessor<MailboxProcessorMsg<'p, 'u, 'r>>) replyFunc =
        agent.PostAndReply(fun reply -> MailboxProcessorMsg.Reply(replyFunc, reply))