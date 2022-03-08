module RandomSceneDrawing.Main

open Elmish

type PlayerId =
    | MainPlayer
    | SubPlayer

type Model<'player> =
    { MainPlayer: Player.Model<'player>
      SubPlayer: Player.Model<'player> }

type Msg = PlayerMsg of PlayerId * Player.Msg


let init player subPlayer =
    { MainPlayer = Player.init player
      SubPlayer = Player.init subPlayer }

let update playerApi msg m =
    let playerUpdate = Player.update playerApi

    match msg with
    | PlayerMsg (MainPlayer, msg) ->
        let mainPlayer', cmd' = playerUpdate msg m.MainPlayer

        { m with MainPlayer = mainPlayer' }, Cmd.map ((fun m -> MainPlayer, m) >> PlayerMsg) cmd'

    | PlayerMsg (SubPlayer, msg) ->
        let player', cmd' = playerUpdate msg m.SubPlayer

        { m with SubPlayer = player' }, Cmd.map ((fun m -> SubPlayer, m) >> PlayerMsg) cmd'
