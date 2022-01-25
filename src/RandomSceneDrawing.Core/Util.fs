module RandomSceneDrawing.Util

let inline tap ([<InlineIfLambda>] sideEffect) n =
    sideEffect n
    n
