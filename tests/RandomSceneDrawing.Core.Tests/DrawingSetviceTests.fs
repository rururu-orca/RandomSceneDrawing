module RandomSceneDrawing.Tests

open Expecto
open RandomSceneDrawing.DrawingSetvice

[<Tests>]
let tests =
    testList
        "DrawingSetvice"
        [ test "A simple countdown" {
              let m =
                  { Frames = 1
                    Duration = TimeSpan.ofSec 30.0<sec>
                    Interval = 1 }

              let updated = update (TickSec 1.0<sec>) m

              "Model can countdown from Message"
              |> Expect.equal updated.Duration (TimeSpan.ofSec 29.0<sec>)
          } ]
