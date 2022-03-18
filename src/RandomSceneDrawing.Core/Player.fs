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

type api<'player> =
    { playAsync: 'player -> Task<Msg>
      pauseAsync: 'player -> Task<Msg>
      stopAsync: 'player -> Task<Msg> }

module ApiMock =
    let okMediaInfo = Ok { Title = ""; Duration = TimeSpan.Zero }

    let apiOk =
        { playAsync = fun _ -> task { return Play(Finished okMediaInfo) }
          pauseAsync = fun _ -> task { return Pause(Finished okMediaInfo) }
          stopAsync = fun _ -> task { return Stop(Finished(Ok())) } }

    let errorResult = Error "Not Implemented"
    let errorFinished = Finished errorResult
    let errorResolved = Resolved errorResult

    let apiError =
        { playAsync = fun _ -> task { return Play errorFinished }
          pauseAsync = fun _ -> task { return Pause errorFinished }
          stopAsync = fun _ -> task { return Stop errorFinished } }


open Elmish
open Elmish.Cmd.OfTask

let init player =
    { Player = player
      State = Finished(Ok Stopped)
      Media = HasNotStartedYet }

let update api msg m =
    let mapState state = Result.map (fun _ -> state)

    match msg, m.Media with
    | Play Started, InProgress -> m, Cmd.none
    | Play Started, _ ->
        { m with
            Media = InProgress
            State = Started },
        api.playAsync m.Player |> result
    | Play (Finished result), _ ->
        { m with
            Media = Resolved result
            State = Finished(mapState Playing result) },
        Cmd.none
    | Pause Started, HasNotStartedYet
    | Pause Started, InProgress
    | Pause Started, Resolved (Error _) -> m, Cmd.none
    | Pause Started, _ -> { m with State = Started }, api.pauseAsync m.Player |> result
    | Pause (Finished (Ok mediainfo)), _ ->
        { m with
            Media = Resolved(Ok mediainfo)
            State = Finished(Ok Paused) },
        Cmd.none
    | Pause (Finished (Error msg)), _ -> { m with State = Finished(Error msg) }, Cmd.none
    | Stop Started, HasNotStartedYet
    | Stop Started, InProgress
    | Stop Started, Resolved (Error _) -> m, Cmd.none
    | Stop Started, _ -> { m with State = Started }, api.stopAsync m.Player |> result
    | Stop (Finished (Ok _)), _ ->
        { m with
            Media = HasNotStartedYet
            State = Finished(Ok Stopped) },
        Cmd.none
    | Stop (Finished (Error msg)), _ -> { m with State = Finished(Error msg) }, Cmd.none
