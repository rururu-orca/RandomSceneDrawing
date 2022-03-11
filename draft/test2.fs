open System

/// Dispatch - feed new message into the processing loop
type Dispatch<'msg> = 'msg -> unit

/// Subscription - return immediately, but may schedule dispatch of a message at any time
type Sub<'msg> = Dispatch<'msg> -> unit

/// Cmd - container for subscriptions that may produce messages
type Cmd<'msg> = Sub<'msg> list



type State =
    { Count : int }

type Msg =
    | Increment
    | Decrement

let update (msg: Msg) (state: State): State =
    match msg with
    | Increment ->
        { state with Count = state.Count + 1 }

    | Decrement ->
        { state with Count = state.Count - 1 }

let dispatchImpl :Dispatch<'msg>  = fun msg -> printfn $"{msg}"
let cmd:Cmd<_> = [fun dispatch -> dispatch Increment]
cmd |> List.iter (fun sub -> sub dispatchImpl )
let ms msg :Sub<'msg>= fun (dispatch:Dispatch<'msg>) ->
    dispatch msg
ms Increment dispatchImpl

[<Struct>]
type Validated<'value, 'wrapped, 'error> =
    private
    | Valid of wrapped: 'wrapped
    | Invalid of current: 'wrapped voption * arg: 'value * error: 'error

[<Struct>]
type UnwrapInvalid<'value, 'error> =
    { Current: 'value voption
      Arg: 'value
      Error: 'error }

let (|Valid|Invalid|) (validated: Validated<'value, 'wrapped, 'error>) =
    match validated with
    | Valid value -> Valid value
    | Invalid (current, arg, error) -> Invalid(current, arg, error)

type Point2D = { X: float; Y: float; } with
  static member add (lhs: Point2D) ( rhs: Point2D) =
    { X = lhs.X + rhs.X; Y = lhs.Y + rhs.Y; }

module V2 =
    let inline (|Id|) x =
        fun () -> (^a : (member Id: string) x)

    let inline id (Id f) = f()

module T =
    let inline (|Id|) x =
        fun () -> (^a : (member Id: string) x)

    let inline id (Id f) = f()

    let inline test< ^a> param =
        let aT = typeof< ^a>.Name
        let idStr = id param
        sprintf "%s %s" aT idStr

    type C =
        { Name: string }
        with
        member __.Id = "1"

    let c = { Name = "abc" }

    let b = test<int> c

type Validator<'value, 'wrapped, 'error>(wrap, unwrap, validate) =
    let validate: 'value -> Result<'value, 'error> = validate
    let wrap: 'value -> 'wrapped = wrap
    let unwrap: 'wrapped -> 'value = unwrap

    member _.Create value =
        match validate value with
        | Ok v -> Valid(wrap v)
        | Error error -> Invalid(ValueNone, value, error)

    member _.Update newValueArg (currentValue: Validated<'value, 'wrapped, 'error>) =
        match (validate newValueArg), currentValue with
        | Ok v, _ -> Valid(wrap v)
        | Error error, Valid c -> Invalid(ValueSome c, newValueArg, error)
        | Error error, Invalid _ -> Invalid(ValueNone, newValueArg, error)

    member this.Map (f: 'value -> 'value) validated =
        match validated with
        | Valid v -> validated |> this.Update(f (unwrap v))
        | invalid -> invalid

    member _.UnwrapWith (f: 'value -> 'a) (validated: Validated<'value, 'wrapped, 'error>) =
        match validated with
        | Valid v -> Ok(f (unwrap v))
        | Invalid ((ValueSome v), invalid, error) ->
            Error
                { Current = ValueSome(unwrap v)
                  Arg = invalid
                  Error = error }
        | Invalid (ValueNone, invalid, error) ->
            Error
                { Current = ValueNone
                  Arg = invalid
                  Error = error }

    member this.Unwrap(validated: Validated<'value, 'wrapped, 'error>) = this.UnwrapWith id validated

    member _.Iter f (validated: Validated<'value, 'wrapped, 'error>) =
        match validated with
        | Valid v -> f (unwrap v)
        | _ -> ()

module Num2 =
    type Num2<'t> = private Num2 of 't
    let (|Num2|) (Num2 v) = v

    let createNum2 validate =
        Validator(Num2, (fun (Num2 v) -> v), validate)

    let valudate limit i = if i <= limit then Ok i else Error ""
    let num2Int = valudate 2 |> createNum2
    let num2Float = valudate 2.0 |> createNum2

open Num2

module V =
    type Frames = private Frames of int
    let (|Frames|) (Frames i) = i

    let frames =
        Validator(
            Frames,
            (fun (Frames i) -> i),
            fun i ->
                if i < 0 then
                    Error "Frames must be positive."
                else
                    Ok i
        )

open V

type Model =
    { Frames: Validated<int, Frames, string> }

let m = { Frames = frames.Create -2 }
m.Frames |> frames.Unwrap
let i = num2Int.Create 2 |> num2Int.Update 1

match i with
| Valid (Num2 n) -> $"Value: {n}"
| _ -> "nope"

num2Int.Create 2
|> num2Int.Map(fun i -> i + 0)
|> num2Int.Unwrap

open System

type Longitude =
    private
    | Longitude of float

    member this.Value = let (Longitude lng) = this in lng

    // float -> Result<Longitude, string>
    static member TryCreate(lng: float) =
        if lng >= -180. && lng <= 180. then
            Ok(Longitude lng)
        else
            sprintf "%A is a invalid longitude value" lng
            |> Error

type Tweet =
    private
    | Tweet of string

    member this.Value = let (Tweet tweet) = this in tweet

    static member TryCreate(tweet: string) =
        if String.IsNullOrEmpty tweet then
            Error "Tweet shouldn't be empty"
        elif tweet.Length > 280 then
            Error "Tweet shouldn't contain more than 280 characters"
        else
            Ok(Tweet tweet)

type Location =
    { Latitude: Latitude
      Longitude: Longitude }

type CreatePostRequest = { Tweet: Tweet; Location: Location }

let location lat lng = { Latitude = lat; Longitude = lng }

let createPostRequest lat long tweet =
    { Tweet = tweet
      Location = location lat long }

type LocationDto = { Latitude: float; Longitude: float }

type CreatePostRequestDto =
    { Tweet: string
      Location: LocationDto }

module M = 
    type Validated'<'value, 'wrapped, 'error when 'wrapped: (static member wrap: 'value -> 'wrapped) and 'wrapped: (static member unwrap:
        'wrapped -> 'value) and 'wrapped: (static member validate: 'value -> Result<'value, 'error>)> =
        private
        | Valid of wrapped: 'wrapped
        | Invalid of current: 'wrapped voption * arg: 'value * error: 'error
        // with
        // static member wrap x = ()

    type BindImpl =
        static member ( >>= ) (m: _ option, f) = Option.bind f m
        static member ( >>= ) (m: _ list,   f) = List.collect f m

    type Bind =
        static member inline Invoke (value: ^Ma, binder: 'a -> ^Mb) : ^Mb =
            // このヘルパー関数は書き方によって警告が出たり出なかったりする
            // 警告が出るのはオーバーロード解決が早く起こりすぎてしまうのが原因だが，
            // オーバーロード解決を遅延させる一般的な方法はよくわからない
            let inline call (_impl: ^impl, m: ^m, _r: ^r, f) =
                ((^impl or ^m or ^r): (static member (>>=): _*_->_) m,f)
            call (Unchecked.defaultof<BindImpl>, value, Unchecked.defaultof< ^Mb >, binder)

    let inline (>>=) m f = Bind.Invoke (m, f)

    type M<'t> = M of 't with
        static member (>>=) (M x, f) : M<_> = f x

    let m1 = Some 2  >>= fun x -> Some (x + 1)
    let m2 = [1;2;3] >>= fun x -> [x; x+1; x+2]
    let m3 = M 2     >>= fun x -> M    (x + 1)

    type Default1 = class inherit Default2 end
    and  Default2 = class inherit Default3 end
    and  Default3 = class end

    type Dummy1<'t>(x: 't) = class member val Value1 = x end
    type Dummy2<'t>(x: 't) = class member val Value2 = x end

