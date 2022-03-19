module RandomSceneDrawing.Player

open System
open System.Threading.Tasks
open RandomSceneDrawing.Types

type State =
    | Playing
    | Paused
    | Stopped

type Model<'player> =
    { Player: 'player
      State: AsyncOperationStatus<Result<State, string>>
      Media: Deferred<Result<MediaInfo, string>> }

type Msg =
    | Play of Result<MediaInfo, string> AsyncOperationStatus
    | Pause of Result<MediaInfo, string> AsyncOperationStatus
    | Stop of Result<unit, string> AsyncOperationStatus

type Api<'player> =
    { playAsync: 'player -> Task<Result<MediaInfo, string>>
      pauseAsync: 'player -> Task<Result<MediaInfo, string>>
      stopAsync: 'player -> Task<Result<unit, string>>
      showInfomation: NotifyMessage -> Task<unit> }

module ApiMock =
    let mediaInfo = { Title = ""; Duration = TimeSpan.Zero }
    let okMediaInfo = Ok mediaInfo

    let apiOk =
        { playAsync = fun _ -> task { return okMediaInfo }
          pauseAsync = fun _ -> task { return okMediaInfo }
          stopAsync = fun _ -> task { return Ok() }
          showInfomation = fun _ -> task { () } }

    let errorResult = Error "Not Implemented"
    let errorFinished = Finished errorResult
    let errorResolved = Resolved errorResult

    let apiError =
        { playAsync = fun _ -> task { return errorResult }
          pauseAsync = fun _ -> task { return errorResult }
          stopAsync = fun _ -> task { return errorResult }
          showInfomation = fun _ -> task { () }}

open Elmish
open FsToolkit.ErrorHandling

type Cmd<'player>(api: Api<'player>, player) =

    let showInfomation info =
        task { do! api.showInfomation info } |> ignore

    member _.ShowInfomation info = showInfomation info

    member _.PlayCmd() =
        task {
            let! info =
                api.playAsync player
                |> TaskResult.teeError (ErrorMsg >> showInfomation)
            return (Finished >> Play) info
        }
        |> Cmd.OfTask.result

    member _.PauseCmd() =
        task {
            let! info = 
                api.pauseAsync player
                |> TaskResult.teeError (ErrorMsg >> showInfomation)
            return (Finished >> Pause) info
        }
        |> Cmd.OfTask.result

    member _.StopCmd() =
        task {
            let! info =
                api.stopAsync player
                |> TaskResult.teeError (ErrorMsg >> showInfomation)
            return (Finished >> Stop) info
        }
        |> Cmd.OfTask.result



let init player =
    { Player = player
      State = Finished(Ok Stopped)
      Media = HasNotStartedYet }

let update api msg m =
    let cmds = Cmd(api, m.Player)
    let mapState state = Result.map (fun _ -> state)

    match msg, m.Media with
    | Play Started, InProgress -> m, Cmd.none
    | Play Started, _ ->
        { m with
            Media = InProgress
            State = Started },
        cmds.PlayCmd()
    | Play (Finished result), _ ->
        { m with
            Media = Resolved result
            State = Finished(mapState Playing result) },
        Cmd.none
    | Pause Started, HasNotStartedYet
    | Pause Started, InProgress
    | Pause Started, Resolved (Error _) -> m, Cmd.none
    | Pause Started, _ -> { m with State = Started }, cmds.PauseCmd()
    | Pause (Finished (Ok mediainfo)), _ ->
        { m with
            Media = Resolved(Ok mediainfo)
            State = Finished(Ok Paused) },
        Cmd.none
    | Pause (Finished (Error msg)), _ -> { m with State = Finished(Error msg) }, Cmd.none
    | Stop Started, HasNotStartedYet
    | Stop Started, InProgress
    | Stop Started, Resolved (Error _) -> m, Cmd.none
    | Stop Started, _ -> { m with State = Started }, cmds.StopCmd()
    | Stop (Finished (Ok _)), _ ->
        { m with
            Media = HasNotStartedYet
            State = Finished(Ok Stopped) },
        Cmd.none
    | Stop (Finished (Error msg)), _ -> { m with State = Finished(Error msg) }, Cmd.none
