module RandomSceneDrawing.Types

open System
open System.Threading.Tasks
open LibVLCSharp


type InvalidType<'dto, 'domain, 'error> =
    | CreateFailed of 'dto * 'error list
    | UpdateFailed of 'domain voption * 'dto * 'error list
    | MargedError of InvalidType<obj, obj, 'error> list

type Validated<'dto, 'domain, 'error> =
    private
    | Valid of domain: 'domain
    | Invalid of error: InvalidType<'dto, 'domain, 'error>

let invalidCreateFailed dto errors = (CreateFailed >> Invalid) (dto, errors)

let invalidUpdateFailed domain dto errors =
    (UpdateFailed >> Invalid) (domain, dto, errors)

let boxInvalid (validated: Validated<'dto, 'domain, 'error>) =
    match validated with
    | Valid _ -> []
    | Invalid ex ->
        [ match ex with
          | CreateFailed (dto, errors) -> CreateFailed(box dto, errors)
          | UpdateFailed (domain, dto, errors) -> UpdateFailed(ValueOption.map box domain, box dto, errors)
          | MargedError marged -> MargedError marged ]

let (|BoxInvalid|) = boxInvalid

let invaidStringList (invalid: InvalidType<'dto, 'domain, 'error>) = $"%A{invalid}"


let margeInvalids validateds =
    [ for v in validateds do
          yield! boxInvalid v ]

let invalidMargedErrors invalids = (MargedError >> Invalid) invalids

/// Controls `Validated<'dto, 'domain, 'error>`.
type Domain<'dto, 'domain, 'error>
    (
        fromDto: 'dto -> 'domain,
        toDto: 'domain -> 'dto,
        validate: 'dto -> Result<'dto, 'error list>
    )
     =

    member _.Create dto : Validated<'dto, 'domain, 'error> =
        match validate dto with
        | Ok v -> Valid(fromDto v)
        | Error error -> invalidCreateFailed dto error

    member this.ofDomain domain = toDto domain |> this.Create

    member _.ToDto domain = toDto domain

    member _.Update newValueArg (currentValue: Validated<'dto, 'domain, 'error>) =
        match (validate newValueArg), currentValue with
        | Ok v, _ -> Valid(fromDto v)
        | Error error, Valid c -> invalidUpdateFailed (ValueSome c) newValueArg error
        | Error error, Invalid (UpdateFailed (ValueSome c, _, _)) -> invalidUpdateFailed (ValueSome c) newValueArg error
        | Error error, Invalid _ -> invalidUpdateFailed ValueNone newValueArg error


    member _.IsValid(validated: Validated<'dto, 'domain, 'error>) =
        match validated with
        | Valid _ -> true
        | Invalid _ -> false

    member _.IsInvalid(validated: Validated<'dto, 'domain, 'error>) =
        match validated with
        | Valid _ -> false
        | Invalid _ -> true

    member _.Fold onValid onInvalid (validated: Validated<'dto, 'domain, 'error>) =
        match validated with
        | Valid x -> onValid x
        | Invalid ex -> onInvalid ex

    member this.ToResult validated = this.Fold(toDto >> Ok) Error validated

    member _.ValueWith f (domain: 'domain) = f domain
    member _.DtoWith f (dto: 'dto) = f dto

    member this.ValueOr f validated = this.Fold id f validated
    member this.DtoOr f validated = this.Fold toDto f validated

    member this.Value validated =
        this.ValueOr(fun ex -> invalidOp $"This value is Invalid.\n{ex}") validated

    member this.Dto validated =
        this.DtoOr(fun ex -> invalidOp $"This value is Invalid.\n{ex}") validated

    member this.DefaultWith f validated = this.Fold id (ignore >> f) validated

    member this.DefaultDtoWith f validated = this.Fold toDto (ignore >> f) validated

    member this.DefaultValue value validated =
        this.DefaultWith(fun _ -> value) validated

    member this.DefaultDto value validated =
        this.DefaultDtoWith(fun _ -> value) validated

    member this.Tee f validated = this.Fold f ignore validated

    member this.TeeDto f validated = this.Fold(toDto >> f) ignore validated

    member this.TeeInvalid f validated = this.Fold ignore f validated

    member _.Map f validated : Validated<'dto, 'domain, 'error> =
        match validated with
        | Valid x ->
            let x' = (f >> toDto) x

            match validate x' with
            | Ok v -> Valid(fromDto v)
            | Error error -> invalidUpdateFailed (ValueSome x) x' error
        | invalid -> invalid

    member this.MapDto f validated =
        match validated with
        | Valid x -> this.Update((toDto >> f) x) validated
        | invalid -> invalid

    member _.MapInvalid f validated : Validated<'dto, 'domain, 'error> =
        match validated with
        | Invalid ex -> f ex |> Invalid
        | valid -> valid

    member private _.Lift2Proto
        f
        (xValidated: Validated<'dto, 'domain, 'error>)
        (yValidated: Validated<'dto, 'domain, 'error>)
        =
        match xValidated, yValidated with
        | Valid x, Valid y ->
            let x' = f x y

            match validate x' with
            | Ok v -> Valid(fromDto v)
            | Error error -> invalidUpdateFailed (ValueSome x) x' error
        | (Invalid _ as ex), Valid _ -> ex
        | Valid _, (Invalid _ as ex) -> ex
        | (BoxInvalid v), (BoxInvalid v') -> invalidMargedErrors (v @ v')

    member this.Lift2 f (xValidated: Validated<'dto, 'domain, 'error>) (yValidated: Validated<'dto, 'domain, 'error>) =
        this.Lift2Proto(fun x y -> f x y |> toDto) xValidated yValidated

    member this.Lift2Dto
        f
        (xValidated: Validated<'dto, 'domain, 'error>)
        (yValidated: Validated<'dto, 'domain, 'error>)
        =
        this.Lift2Proto(fun x y -> f (toDto x) (toDto y)) xValidated yValidated

    member private _.ApplyProto
        onValid
        (fValidated: Validated<'dto, _ -> _, 'error>)
        (xValidated: Validated<'dto, 'domain, 'error>)
        =
        match fValidated, xValidated with
        | Valid f, Valid x -> onValid f x
        | (BoxInvalid v), Valid _ -> invalidMargedErrors v
        | Valid _, (Invalid _ as ex) -> ex
        | (BoxInvalid v), (BoxInvalid v') -> invalidMargedErrors (v @ v')

    member this.Apply
        (fValidated: Validated<'dto, 'domain -> 'domain, 'error>)
        (xValidated: Validated<'dto, 'domain, 'error>)
        =
        this.ApplyProto
            (fun f x ->
                let x' = (f >> toDto) x

                match validate x' with
                | Ok v -> Valid(fromDto v)
                | Error error -> invalidUpdateFailed (ValueSome x) x' error)
            fValidated
            xValidated

    member this.ApplyDto
        (fValidated: Validated<'dto, 'dto -> 'dto, 'error>)
        (xValidated: Validated<'dto, 'domain, 'error>)
        : Validated<'dto, 'domain, 'error>
        =
        this.ApplyProto(fun f x -> this.Update((toDto >> f) x) xValidated) fValidated xValidated

let (|Valid|Invalid|) (validated: Validated<'dto, 'domain, 'error>) =
    match validated with
    | Valid value -> Valid value
    | Invalid ex -> Invalid ex


type NotifyMessage =
    | InfoMsg of string
    | ErrorMsg of string

module ErrorTypes =
    type FilePickerError =
        | Canceled
        | FileSystemError of string

module Validator =

    let validateIfPositiveNumber num =
        if num < 0 then
            Error [ "Must be a positive number." ]
        else
            Ok num

    let validateIfPositiveTime time =
        if time < TimeSpan.Zero then
            Error [ "Must be a positive number." ]
        else
            Ok time

    open System.IO

    type FileType =
        | File
        | Directory

    let validateExists label path =
        match label with
        | File when File.Exists path -> Ok path
        | Directory when Directory.Exists path -> Ok path
        | _ -> Error [ $"{path} is not exsists." ]

    let validatePathString label path =
        if String.IsNullOrEmpty path then
            Error ["NullOrEmpty"]
        else
            validateExists label path

let resultDtoOr (domain: Domain<'dto, 'domain, 'error>) value =
    domain.ToResult value
    |> Result.mapError invaidStringList

let resultDomainOr (domain: Domain<'dto, 'domain, 'error>) value =
    domain.Fold Ok Error value
    |> Result.mapError invaidStringList

type AsyncOperationStatus<'t> =
    | Started
    | Finished of 't

type Deferred<'t> =
    | HasNotStartedYet
    | InProgress
    | Resolved of 't


/// Contains utility functions to work with value of the type `Deferred<'T>`.
module Deferred =

    /// Returns whether the `Deferred<'T>` value has been resolved or not.
    let resolved =
        function
        | HasNotStartedYet -> false
        | InProgress -> false
        | Resolved _ -> true

    /// Returns whether the `Deferred<'T>` value is in progress or not.
    let inProgress =
        function
        | HasNotStartedYet -> false
        | InProgress -> true
        | Resolved _ -> false

    /// Transforms the underlying value of the input deferred value when it exists from type to another
    let map (transform: 'T -> 'U) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Resolved value -> Resolved(transform value)

    /// Verifies that a `Deferred<'T>` value is resolved and the resolved data satisfies a given requirement.
    let exists (predicate: 'T -> bool) =
        function
        | HasNotStartedYet -> false
        | InProgress -> false
        | Resolved value -> predicate value

    /// Like `map` but instead of transforming just the value into another type in the `Resolved` case, it will transform the value into potentially a different case of the the `Deferred<'T>` type.
    let bind (transform: 'T -> Deferred<'U>) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Resolved value -> transform value

type DeferredResult<'t, 'error> = Deferred<Result<'t, 'error>>